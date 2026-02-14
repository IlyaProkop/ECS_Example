using System.Collections.Generic;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.ECS.Navigation;
using Game.Spatial;
using Scellecs.Morpeh;
using UnityEngine;
using Unity.Profiling;

namespace Game.ECS.Systems {
    internal sealed class EnemyPathPlanningSystem : ISystem {
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("Arena.System.EnemyPathPlanning");
        private readonly Game.Domain.NavigationSettings config;
        private readonly EntityLookup entities;
        private readonly Game.Spatial.ObstacleGridLookup obstacles;
        private readonly Game.ECS.Navigation.PathStorage pathStorage;
        private readonly System.Random navigationRandom;
        private readonly AStarGridPathfinder pathfinder;
        private readonly List<Vector3> pathBuffer;

        private readonly Entity[] active;
        private int activeCount;
        private int nextStart;
        private Filter enemyFilter;

        private Stash<PositionComponent> positionStash;
        private Stash<HealthComponent> healthStash;
        private Stash<RadiusComponent> radii;
        private Stash<MovementGoalComponent> goals;
        private Stash<EnemyPathComponent> enemyPathStash;
        private Stash<GameStateComponent> gameStateStash;

        public EnemyPathPlanningSystem(Game.Domain.NavigationSettings config, Game.Domain.ArenaSettings arena, int capacity, EntityLookup entities, Game.Spatial.ObstacleGridLookup obstacles, Game.ECS.Navigation.PathStorage pathStorage, System.Random navigationRandom) {
            this.config = config;
            this.entities = entities;
            this.obstacles = obstacles;
            this.pathStorage = pathStorage;
            this.navigationRandom = navigationRandom;
            this.active = new Entity[capacity];
            this.pathfinder = new AStarGridPathfinder(config, arena, obstacles);
            this.pathBuffer = new List<Vector3>(config.enemyPathMaxNodes);
        }

        public World World { get; set; }

        public void OnAwake() {
            this.enemyFilter = this.World.Filter
                .With<EnemyTag>()
                .With<PositionComponent>()
                .With<HealthComponent>()
                .With<MovementGoalComponent>()
                .With<EnemyPathComponent>()
                .Build();

            this.positionStash = this.World.GetStash<PositionComponent>();
            this.healthStash = this.World.GetStash<HealthComponent>();
            this.radii = this.World.GetStash<RadiusComponent>();
            this.goals = this.World.GetStash<MovementGoalComponent>();
            this.enemyPathStash = this.World.GetStash<EnemyPathComponent>();
            this.gameStateStash = this.World.GetStash<GameStateComponent>();
        }

        public void OnUpdate(float deltaTime) {
            using var sample = UpdateMarker.Auto();
            if (!GameStateHelper.IsCombat(this.entities, this.gameStateStash)) return;

            var config = this.config;
            var targetMoveThresholdSqr = config.enemyPathTargetMoveThreshold * config.enemyPathTargetMoveThreshold;
            var waypointReachDistance = Mathf.Max(config.enemyPathWaypointReachDistance, config.enemyPathCellSize * 0.5f);
            var waypointReachDistanceSqr = waypointReachDistance * waypointReachDistance;
            var repathBudget = Mathf.Max(1, config.enemyPathMaxRepathsPerFrame);
            var repathsPerformed = 0;
            var obstacleLookup = this.obstacles.AsNative();

            var previousCount = this.activeCount;
            this.activeCount = 0;
            foreach (var enemy in this.enemyFilter) {
                if (this.healthStash.Get(enemy).current <= 0f) continue;
                this.active[this.activeCount++] = enemy;
                this.enemyPathStash.Get(enemy).repathTimer -= deltaTime;
            }
            if (previousCount > this.activeCount) System.Array.Clear(this.active, this.activeCount, previousCount - this.activeCount);
            if (this.activeCount == 0) return;
            var startIndex = this.nextStart % this.activeCount;
            for (var offset = 0; offset < this.activeCount; offset++) {
                var index = (startIndex + offset) % this.activeCount;
                var enemyEntity = this.active[index];
                var enemyPath = this.enemyPathStash.Get(enemyEntity);
                var enemyPosition = this.positionStash.Get(enemyEntity).value;
                var radius = this.radii.Get(enemyEntity).value;
                var goal = this.goals.Get(enemyEntity);
                var goalPosition = goal.position;
                var stopDistance = goal.stoppingDistance;
                var toGoal = goalPosition - enemyPosition;
                toGoal.y = 0f;
                if (!goal.active || toGoal.sqrMagnitude <= stopDistance * stopDistance) {
                    enemyPath.waypointCount = 0;
                    enemyPath.currentWaypointIndex = 0;
                    enemyPath.lastTargetPosition = goalPosition;
                    enemyPath.status = PathStatus.Direct;
                    this.enemyPathStash.Set(enemyEntity, enemyPath);
                    continue;
                }

                var targetMoved = (goalPosition - enemyPath.lastTargetPosition).sqrMagnitude >= targetMoveThresholdSqr;
                var pathConsumed = enemyPath.status == PathStatus.None ||
                    (enemyPath.status == PathStatus.Route && enemyPath.currentWaypointIndex >= enemyPath.waypointCount);

                if (enemyPath.repathTimer > 0f && !targetMoved && !pathConsumed) {
                    this.enemyPathStash.Set(enemyEntity, enemyPath);
                    continue;
                }

                if (ObstacleQueries.HasLineOfSight(enemyPosition, goalPosition, radius, obstacleLookup)) {
                    enemyPath.status = PathStatus.Direct;
                    enemyPath.waypointCount = 0;
                    enemyPath.currentWaypointIndex = 0;
                    enemyPath.repathTimer = NextRepathDelay(config.enemyPathRepathInterval, config.enemyPathRepathJitter);
                    enemyPath.lastTargetPosition = goalPosition;
                    this.enemyPathStash.Set(enemyEntity, enemyPath);
                    continue;
                }

                if (repathsPerformed >= repathBudget) {
                    this.nextStart = index;
                    break;
                }
                repathsPerformed++;
                this.nextStart = (index + 1) % this.activeCount;
                enemyPath.repathTimer = NextRepathDelay(config.enemyPathRepathInterval, config.enemyPathRepathJitter);
                enemyPath.currentWaypointIndex = 0;
                enemyPath.lastTargetPosition = goalPosition;

                this.pathBuffer.Clear();
                var hasRoute = this.pathfinder.TryFindPath(
                    enemyPosition,
                    goalPosition,
                    radius,
                    this.pathBuffer);

                if (!hasRoute || this.pathBuffer.Count == 0) {
                    enemyPath.status = PathStatus.Blocked;
                    enemyPath.waypointCount = 0;
                    this.enemyPathStash.Set(enemyEntity, enemyPath);
                    continue;
                }

                var firstUsefulWaypoint = this.pathBuffer.Count > 1 ? 1 : 0;
                enemyPath.status = PathStatus.Route;
                enemyPath.waypointCount = this.pathStorage.Write(enemyPath.slot, this.pathBuffer, firstUsefulWaypoint);
                while (enemyPath.currentWaypointIndex < enemyPath.waypointCount) {
                    var waypoint = this.pathStorage.Get(enemyPath.slot, enemyPath.currentWaypointIndex);
                    var delta = waypoint - enemyPosition;
                    delta.y = 0f;
                    if (delta.sqrMagnitude > waypointReachDistanceSqr) {
                        break;
                    }

                    enemyPath.currentWaypointIndex++;
                }

                this.enemyPathStash.Set(enemyEntity, enemyPath);
            }
        }

        public void Dispose() {
            this.pathBuffer.Clear();
            System.Array.Clear(this.active, 0, this.active.Length);
        }

        private float NextRepathDelay(float baseInterval, float jitter) {
            var clampedInterval = Mathf.Max(0f, baseInterval);
            if (clampedInterval <= 0.000001f) {
                return 0f;
            }

            var clampedJitter = Mathf.Clamp01(jitter);
            if (clampedJitter <= 0.000001f) {
                return clampedInterval;
            }

            var factor = 1f + ((float)this.navigationRandom.NextDouble() * 2f - 1f) * clampedJitter;
            return Mathf.Max(0.01f, clampedInterval * factor);
        }

    }
}

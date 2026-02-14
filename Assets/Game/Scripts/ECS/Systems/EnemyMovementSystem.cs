using System;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.ECS.Movement;
using Scellecs.Morpeh;
using UnityEngine;
using Unity.Profiling;

namespace Game.ECS.Systems {
    // Morpeh adapter: prepare values and paths -> solve the batch -> apply results.
    // All three phases stay inside one ISystem, preserving the existing commit boundaries.
    internal sealed class EnemyMovementSystem : ISystem {
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("Arena.System.EnemyMovement");
        private static readonly ProfilerMarker PrepareMarker = new ProfilerMarker("Arena.Motion.Prepare");
        private static readonly ProfilerMarker SolveMarker = new ProfilerMarker("Arena.Motion.Solve");
        private static readonly ProfilerMarker ApplyMarker = new ProfilerMarker("Arena.Motion.Apply");
        private static readonly ProfilerMarker SnapshotMarker = new ProfilerMarker("Arena.Motion.Snapshot");
        private readonly Game.Domain.NavigationSettings config;
        private readonly Vector2 arenaHalfSize;
        private readonly EntityLookup entities;
        private readonly EnemyMotionGeometry geometry;
        private readonly Game.Spatial.ObstacleGridLookup obstacles;
        private readonly Game.ECS.Navigation.PathStorage pathStorage;
        private readonly EnemyMotionInput[] inputs;
        private readonly EnemyMotionResult[] results;
        private readonly Entity[] targets;
        private const int ParallelThreshold = 256;
        private readonly EnemyMotionJobRunner jobRunner;
        private Filter enemies;
        private Stash<PositionComponent> positions;
        private Stash<EnemyVelocityComponent> velocities;
        private Stash<MoveSpeedComponent> speeds;
        private Stash<MovementGoalComponent> goals;
        private Stash<EnemyPathComponent> paths;
        private Stash<RadiusComponent> radii;
        private Stash<HealthComponent> health;
        private Stash<GameStateComponent> states;
        private Stash<ActorComponent> identities;

        public EnemyMovementSystem(Game.Domain.NavigationSettings config, Game.Domain.ArenaSettings arena, int capacity, EntityLookup entities, Game.ECS.Navigation.PathStorage pathStorage, Game.Spatial.ObstacleGridLookup obstacles) {
            this.config = config;
            this.arenaHalfSize = arena.HalfSize;
            this.entities = entities;
            this.pathStorage = pathStorage;
            this.obstacles = obstacles;
            this.inputs = new EnemyMotionInput[capacity];
            this.results = new EnemyMotionResult[capacity];
            this.targets = new Entity[capacity];
            this.geometry = new EnemyMotionGeometry(capacity, capacity >= ParallelThreshold);
            if (capacity >= ParallelThreshold) {
                try {
                    this.jobRunner = new EnemyMotionJobRunner(capacity);
                } catch { this.Dispose(); throw; }
            }
        }
        public World World { get; set; }
        public void OnAwake() {
            this.geometry.Initialize(this.World);
            this.enemies = this.World.Filter.With<EnemyTag>().With<PositionComponent>().With<EnemyPathComponent>().With<MovementGoalComponent>().Without<DestroyTag>().Build();
            this.positions = this.World.GetStash<PositionComponent>();
            this.velocities = this.World.GetStash<EnemyVelocityComponent>();
            this.speeds = this.World.GetStash<MoveSpeedComponent>();
            this.goals = this.World.GetStash<MovementGoalComponent>();
            this.paths = this.World.GetStash<EnemyPathComponent>();
            this.radii = this.World.GetStash<RadiusComponent>();
            this.health = this.World.GetStash<HealthComponent>();
            this.states = this.World.GetStash<GameStateComponent>();
            this.identities = this.World.GetStash<ActorComponent>();
        }
        public void OnUpdate(float deltaTime) {
            using var sample = UpdateMarker.Auto();
            if (deltaTime <= 0f || !GameStateHelper.IsCombat(this.entities, this.states)) return;
            var player = this.entities.player;
            var playerPosition = this.positions.Get(player).value;
            var playerRadius = this.radii.Get(player).value;
            var count = 0;
            try {
                using (PrepareMarker.Auto()) this.Prepare(ref count);
                float maxRadius;
                using (PrepareMarker.Auto()) {
                    using var snapshotSample = SnapshotMarker.Auto();
                    maxRadius = this.geometry.Capture(count >= ParallelThreshold);
                }
                var frame = new EnemyMotionFrame(playerPosition, playerRadius, maxRadius, deltaTime);
                if (count >= ParallelThreshold) {
                    this.jobRunner.Solve(this.inputs, this.results, count, frame,
                        this.geometry.Native, this.arenaHalfSize, this.obstacles.AsNative());
                } else {
                    using (SolveMarker.Auto()) EnemyMotionSolver.Solve(this.inputs.AsSpan(0, count), this.results.AsSpan(0, count),
                        frame, this.geometry.Managed, this.arenaHalfSize, this.obstacles.AsNative());
                }
                // No structural changes or external callbacks may occur between Prepare and Apply.
                using var applySample = ApplyMarker.Auto();
                for (var i = 0; i < count; i++) {
                    var enemy = this.targets[i];
                    this.positions.Get(enemy).value = this.results[i].Position;
                    this.velocities.Get(enemy).value = this.results[i].Velocity;
                }
            } finally {
                // Do not retain removed Morpeh entity objects in reusable working storage.
                Array.Clear(this.targets, 0, count);
            }
        }

        private void Prepare(ref int count) {
            var reach = Mathf.Max(this.config.enemyPathWaypointReachDistance, this.config.enemyPathCellSize * 0.5f);
            var squaredReach = reach * reach;
            foreach (var enemy in this.enemies) {
                if (this.health.Get(enemy).current <= 0f) continue;
                if (count == this.inputs.Length) throw new InvalidOperationException("Enemy motion capacity exceeded.");
                var start = this.positions.Get(enemy).value;
                ref var path = ref this.paths.Get(enemy);
                var desired = PathSteering.Resolve(start, this.goals.Get(enemy), this.speeds.Get(enemy).value,
                    ref path, this.pathStorage, squaredReach);
                this.inputs[count] = new EnemyMotionInput(this.identities.Get(enemy).id, start,
                    this.velocities.Get(enemy).value, desired, this.radii.Get(enemy).value,
                    this.speeds.Get(enemy).value);
                this.targets[count++] = enemy;
            }
        }
        public void Dispose() {
            this.geometry?.Dispose();
            this.jobRunner?.Dispose();
        }
    }
}

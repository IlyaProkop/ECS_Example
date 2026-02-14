using Game.ECS.Components;
using Game.ECS.Core;
using Game.Spatial;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Systems {
    internal sealed class PlayerEnemySeparationSystem : ISystem {
        private readonly Game.Domain.SimulationSettings config;
        private readonly EntityLookup entities;
        private readonly Game.Spatial.EnemySpatialIndex spatial;
        private readonly ObstacleGridLookup obstacles;

        private Stash<PositionComponent> positionStash;
        private Stash<RadiusComponent> radiusStash;
        private Stash<HealthComponent> healthStash;
        private Stash<GameStateComponent> gameStateStash;

        public PlayerEnemySeparationSystem(Game.Domain.SimulationSettings config, EntityLookup entities, Game.Spatial.EnemySpatialIndex spatial, ObstacleGridLookup obstacles) {
            this.config = config;
            this.entities = entities;
            this.spatial = spatial;
            this.obstacles = obstacles;
        }

        public World World { get; set; }

        public void OnAwake() {
            this.positionStash = this.World.GetStash<PositionComponent>();
            this.radiusStash = this.World.GetStash<RadiusComponent>();
            this.healthStash = this.World.GetStash<HealthComponent>();
            this.gameStateStash = this.World.GetStash<GameStateComponent>();
        }

        public void OnUpdate(float deltaTime) {
            if (GameStateHelper.IsGameOver(this.entities, this.gameStateStash)) {
                return;
            }

            if (!this.entities.TryGetPlayer(out var playerEntity) ||
                !this.positionStash.Has(playerEntity) ||
                !this.radiusStash.Has(playerEntity) ||
                !this.healthStash.Has(playerEntity)) {
                return;
            }

            if (this.healthStash.Get(playerEntity).current <= 0f) {
                return;
            }

            var playerPosition = this.positionStash.Get(playerEntity).value;
            var correctedPosition = playerPosition;
            var playerRadius = this.radiusStash.Get(playerEntity).value;
            var queryRadius = playerRadius + this.spatial.MaxRadius + 0.2f;

            var wasAdjusted = false;
            foreach (var sample in this.spatial.QueryCircle(playerPosition, queryRadius)) {
                if (!sample.IsAlive) continue;
                var enemyPos2D = sample.Position;
                var minDistance = playerRadius + sample.Radius;

                var enemyPosition = new Vector3(enemyPos2D.x, correctedPosition.y, enemyPos2D.y);
                var delta = correctedPosition - enemyPosition;
                delta.y = 0f;
                var distanceSqr = delta.sqrMagnitude;
                if (distanceSqr >= minDistance * minDistance) {
                    continue;
                }

                var distance = Mathf.Sqrt(distanceSqr);
                var push = minDistance - distance;
                var direction = distance > 0.00001f ? delta / distance : Vector3.left;
                correctedPosition += direction * push;
                wasAdjusted = true;
            }

            if (!wasAdjusted) {
                return;
            }

            var motion = new ActorMotionQuery(this.config.arenaHalfSize, playerRadius, true, this.obstacles.AsNative());
            this.positionStash.Get(playerEntity).value = motion.Move(playerPosition, correctedPosition - playerPosition);
        }

        public void Dispose() {
        }
    }
}

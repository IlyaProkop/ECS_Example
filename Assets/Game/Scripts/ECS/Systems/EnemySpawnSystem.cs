using Game.ECS.Spawning;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.Spatial;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Systems {
    internal sealed class EnemySpawnSystem : ISystem {
        private readonly Game.Domain.SimulationSettings config;
        private readonly EntityLookup entities;
        private readonly System.Random spawnRandom;
        private readonly IEnemySpawner spawner;
        private readonly ObstacleGridLookup obstacles;
        private Stash<GameStateComponent> states;
        private Stash<PositionComponent> positions;
        private Stash<RadiusComponent> radii;
        private Filter enemies;
        public EnemySpawnSystem(Game.Domain.SimulationSettings config, EntityLookup entities, System.Random spawnRandom, IEnemySpawner spawner, ObstacleGridLookup obstacles) {
            this.config = config;
            this.entities = entities;
            this.spawnRandom = spawnRandom;
            this.spawner = spawner;
            this.obstacles = obstacles;
        }
        public World World { get; set; }
        public void OnAwake() {
            this.states = this.World.GetStash<GameStateComponent>();
            this.positions = this.World.GetStash<PositionComponent>();
            this.radii = this.World.GetStash<RadiusComponent>();
            this.enemies = this.World.Filter.With<EnemyTag>().With<PositionComponent>().Without<DestroyTag>().Build();
        }
        public void OnUpdate(float deltaTime) {
            if (!GameStateHelper.IsCombat(this.entities, this.states)) return;
            var stateEntity = this.entities.gameState;
            var state = this.states.Get(stateEntity);
            var config = this.config;
            if (state.spawnedEnemies >= config.enemyCount) return;
            state.spawnTimer -= deltaTime;
            if (state.spawnTimer > 0f) {
                this.states.Set(stateEntity, state);
                return;
            }
            var playerPosition = this.positions.Get(this.entities.player).value;
            var definition = config.EnemyForSpawn(state.spawnedEnemies);
            var radius = definition.Radius;
            var placement = new ActorMotionQuery(config.arenaHalfSize, radius, true, this.obstacles.AsNative());
            var playerClearance = Mathf.Max(4f, radius + this.radii.Get(this.entities.player).value);
            var extent = config.arenaHalfSize - Vector2.one * (radius + 0.5f);
            for (var attempt = 0; attempt < 64; attempt++) {
                var x = ((float)this.spawnRandom.NextDouble() * 2f - 1f) * extent.x;
                var z = ((float)this.spawnRandom.NextDouble() * 2f - 1f) * extent.y;
                var candidate = new Vector3(x, 0f, z);
                if ((candidate - playerPosition).sqrMagnitude < playerClearance * playerClearance || placement.IsBlocked(candidate)) continue;
                var occupied = false;
                foreach (var enemy in this.enemies) {
                    var clearance = radius + this.radii.Get(enemy).value;
                    if ((this.positions.Get(enemy).value - candidate).sqrMagnitude < clearance * clearance) {
                        occupied = true;
                        break;
                    }
                }
                if (occupied) continue;
                this.spawner.Spawn(candidate, definition);
                state.spawnedEnemies++;
                state.spawnTimer = config.enemySpawnInterval;
                this.states.Set(stateEntity, state);
                return;
            }
            // Placement failure does not consume the finite wave budget.
            state.spawnTimer = 0.1f;
            this.states.Set(stateEntity, state);
        }
        public void Dispose() { }
    }
}

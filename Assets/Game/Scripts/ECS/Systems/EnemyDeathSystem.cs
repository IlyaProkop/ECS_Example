using Game.ECS.Spawning;
using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;

namespace Game.ECS.Systems {
    // One lifetime transition: record kill, create loot, mark for cleanup.
    internal sealed class EnemyDeathSystem : ISystem {
        private readonly Game.Domain.SimulationSettings config;
        private readonly EntityLookup entities;
        private readonly System.Random lootRandom;
        private readonly ICoinSpawner spawner;
        private Filter enemies;
        private Stash<HealthComponent> health;
        private Stash<PositionComponent> positions;
        private Stash<DestroyTag> destroyed;
        private Stash<BattleStatisticsComponent> statistics;
        public EnemyDeathSystem(Game.Domain.SimulationSettings config, EntityLookup entities, System.Random lootRandom, ICoinSpawner spawner) {
            this.config = config;
            this.entities = entities;
            this.lootRandom = lootRandom; this.spawner = spawner;
        }
        public World World { get; set; }
        public void OnAwake() {
            this.enemies = this.World.Filter.With<EnemyTag>().With<HealthComponent>().Without<DestroyTag>().Build();
            this.health = this.World.GetStash<HealthComponent>();
            this.positions = this.World.GetStash<PositionComponent>();
            this.destroyed = this.World.GetStash<DestroyTag>();
            this.statistics = this.World.GetStash<BattleStatisticsComponent>();
        }
        public void OnUpdate(float deltaTime) {
            foreach (var enemy in this.enemies) {
                if (this.health.Get(enemy).current > 0f) continue;
                var position = this.positions.Get(enemy).value;
                this.destroyed.Add(enemy);
                this.statistics.Get(this.entities.gameState).value.kills++;
                if (this.lootRandom.NextDouble() < this.config.coinDropChance) this.spawner.Spawn(position);
            }
        }
        public void Dispose() { }
    }
}

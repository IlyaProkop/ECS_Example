using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;

namespace Game.ECS.Systems {
    internal sealed class ApplyGameOverSystem : ISystem {
        private readonly Game.Domain.SimulationSettings config;
        private readonly EntityLookup entities;
        private Filter enemies;
        private Stash<GameStateComponent> states;
        private Stash<HealthComponent> health;
        public ApplyGameOverSystem(Game.Domain.SimulationSettings config, EntityLookup entities) {
            this.config = config;
            this.entities = entities;
        }
        public World World { get; set; }
        public void OnAwake() {
            this.enemies = this.World.Filter.With<EnemyTag>().With<HealthComponent>().Without<DestroyTag>().Build();
            this.states = this.World.GetStash<GameStateComponent>();
            this.health = this.World.GetStash<HealthComponent>();
        }
        public void OnUpdate(float deltaTime) {
            if (!GameStateHelper.IsCombat(this.entities, this.states)) return;
            ref var state = ref this.states.Get(this.entities.gameState);
            // Defeat has priority when the player and the final enemy die in the same tick.
            if (this.health.Get(this.entities.player).current <= 0f) {
                state.outcome = BattleOutcome.Defeat;
            } else {
                if (state.spawnedEnemies < this.config.enemyCount) return;
                foreach (var enemy in this.enemies) {
                    if (this.health.Get(enemy).current > 0f) return;
                }
                state.outcome = BattleOutcome.Victory;
            }
            state.phase = SessionPhase.Results;
            state.resultsTimer = this.config.resultsDelay;
        }
        public void Dispose() { }
    }
}

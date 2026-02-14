using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;

namespace Game.ECS.Systems {
    internal sealed class CombatStatisticsSystem : ISystem {
        private readonly EntityLookup entities;
        private readonly FrameBuffer<DamageApplied> applied;
        private Stash<BattleStatisticsComponent> statistics;
        public CombatStatisticsSystem(EntityLookup entities, FrameBuffer<DamageApplied> applied) {
            this.entities = entities; this.applied = applied;
        }
        public World World { get; set; }
        public void OnAwake() => this.statistics = this.World.GetStash<BattleStatisticsComponent>();
        public void OnUpdate(float deltaTime) {
            ref var stats = ref this.statistics.Get(this.entities.gameState).value;
            foreach (ref readonly var hit in this.applied.Items) {
                if (hit.sourceTeam == Team.Player) {
                    stats.damageDealt += hit.amount;
                    if (hit.critical) stats.criticalHits++;
                }
                if (hit.targetIsPlayer) stats.damageReceived += hit.amount;
            }
            this.applied.Clear();
        }
        public void Dispose() { }
    }
}

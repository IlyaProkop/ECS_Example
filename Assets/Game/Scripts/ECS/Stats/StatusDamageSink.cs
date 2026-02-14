using Game.Domain.Stats;
using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;

namespace Game.ECS.Stats {
    internal interface IStatusDamageSink {
        void Tick(Entity target, in DamageSourceComponent attribution, PeriodicDamageDefinition definition);
    }
    // Periodic damage shares health authority, armor and statistics with every other attack.
    internal sealed class StatusDamageSink : IStatusDamageSink {
        private readonly FrameBuffer<DamageRequest> requests;
        public StatusDamageSink(FrameBuffer<DamageRequest> requests) => this.requests = requests;
        public void Tick(Entity target, in DamageSourceComponent attribution, PeriodicDamageDefinition definition) {
            var source = attribution;
            source.criticalChance = 0f; // Ticks do not reroll the original shot's critical hit.
            this.requests.Add(new DamageRequest { target = target, source = source, value = definition.Damage, kind = definition.Kind });
        }
    }
}

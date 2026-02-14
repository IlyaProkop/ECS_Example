using Game.Domain.Abilities;
using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;

namespace Game.ECS.Abilities {
    internal sealed class AbilityFactsSystem : ISystem {
        private readonly EntityLookup entities;
        private readonly FrameBuffer<AbilityCast> casts;
        private readonly FrameBuffer<AbilityCastEvent> events;
        private Stash<BattleStatisticsComponent> statistics;
        private long sequence;
        public World World { get; set; }
        public AbilityFactsSystem(EntityLookup entities, FrameBuffer<AbilityCast> casts, FrameBuffer<AbilityCastEvent> events) {
            this.entities = entities; this.casts = casts; this.events = events;
        }
        public void OnAwake() => this.statistics = this.World.GetStash<BattleStatisticsComponent>();
        public void OnUpdate(float deltaTime) {
            try {
                foreach (ref readonly var cast in this.casts.Items) {
                    this.events.Add(new AbilityCastEvent(++this.sequence, cast.abilityId, cast.position));
                    if (ReferenceEquals(cast.caster, this.entities.player)) this.statistics.Get(this.entities.gameState).value.abilityUses++;
                }
            } finally { this.casts.Clear(); }
        }
        public void Dispose() { }
    }
}

using Game.Domain.Stats;
using Game.ECS.Components;
using Game.ECS.Stats;
using Scellecs.Morpeh;

namespace Game.ECS.Abilities {
    internal sealed class SelfStatusEffect : IAbilityEffect {
        private readonly StatStorage stats;
        private readonly CompiledStatus definition;
        private readonly Stash<HealthComponent> health;
        private readonly Stash<DestroyTag> destroyed;
        public SelfStatusEffect(SelfStatusDefinition definition, World world, StatStorage stats) {
            this.stats = stats;
            this.definition = stats.Compile(definition.Status);
            this.health = world.GetStash<HealthComponent>();
            this.destroyed = world.GetStash<DestroyTag>();
        }
        public void Execute(in AbilityCast cast) {
            if (cast.caster.IsNullOrDisposed() || !this.health.Has(cast.caster) || this.health.Get(cast.caster).current <= 0f || this.destroyed.Has(cast.caster)) return;
            this.stats.TryApply(cast.caster, new EffectSource(cast.source.owner, this.definition.Definition.Id), this.definition, cast.source, out _);
        }
        public void Dispose() { }
    }
}

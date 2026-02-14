using Game.Domain.Abilities;
using Game.ECS.Core;
using Game.Spatial;
using Scellecs.Morpeh;

namespace Game.ECS.Abilities {
    internal static class AbilityModule {
        public static AbilityEffectRegistry CreateRegistry(World world, EnemySpatialIndex spatial,
            ObstacleGridLookup obstacles, FrameBuffer<DamageRequest> requests, Stats.StatStorage stats) {
            var registry = new AbilityEffectRegistry();
            var targets = new HostileAreaQuery(world, spatial, obstacles);
            registry.Register<AreaDamageDefinition>(definition =>
                new AreaDamageEffect(definition, targets, requests));
            registry.Register<Game.Domain.Stats.SelfStatusDefinition>(definition => new SelfStatusEffect(definition, world, stats));
            registry.Register<Game.Domain.Stats.AreaStatusDefinition>(definition => new AreaStatusEffect(definition, world, stats, obstacles));
            return registry;
        }

        public static void Install(System.Action<ISystem> add, EntityLookup entities, System.Collections.Generic.IEnumerable<AbilityDefinition> definitions,
            AbilityEffectRegistry registry, FrameBuffer<AbilityCast> casts, FrameBuffer<AbilityCastEvent> events) {
            var execution = new AbilityExecutionSystem(casts, definitions, registry);
            add(new PlayerAbilityIntentSystem(entities));
            add(new AbilityActivationSystem(entities, casts));
            add(execution);
            add(new AbilityFactsSystem(entities, casts, events));
        }
    }
}

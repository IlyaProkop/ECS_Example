using Game.Domain;
using Game.ECS.Core;
using Game.ECS.Stats;

namespace Game.ECS.Projectiles {
    internal static class ProjectileModule {
        public static EffectRegistry<ProjectileHitDefinition, IProjectileHitEffect> CreateRegistry(StatStorage stats, FrameBuffer<DamageRequest> requests) {
            var registry = new EffectRegistry<ProjectileHitDefinition, IProjectileHitEffect>();
            registry.Register<DirectProjectileDamage>(definition => new ProjectileDamageEffect(definition, requests));
            registry.Register<ProjectileStatusHit>(definition => new ProjectileStatusEffect(definition, stats));
            return registry;
        }
    }
}

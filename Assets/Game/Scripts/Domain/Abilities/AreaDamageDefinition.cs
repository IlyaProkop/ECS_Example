using System;

namespace Game.Domain.Abilities {
    public sealed class AreaDamageDefinition : AbilityEffectDefinition {
        public float Radius { get; }
        public float Damage { get; }
        public AreaDamageDefinition(float radius, float damage) {
            if (float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f) throw new ArgumentOutOfRangeException(nameof(radius));
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage < 0f) throw new ArgumentOutOfRangeException(nameof(damage));
            this.Radius = radius;
            this.Damage = damage;
        }
        public override int GetDamageCapacity(AbilityTargetBudget targets) => targets.Hostiles;
        public override int GetDamageCapacity(int targetCapacity) => targetCapacity;
    }

    public static class BuiltInAbilities {
        public const int ShockwaveId = 1;
        public const float ShockwaveCooldown = 5f;
        public const float ShockwaveRadius = 4.5f;
        public const float ShockwaveDamage = 65f;
        public static AbilityDefinition CreateShockwave(float cooldown = ShockwaveCooldown, float radius = ShockwaveRadius, float damage = ShockwaveDamage) =>
            new AbilityDefinition(ShockwaveId, cooldown, new AreaDamageDefinition(radius, damage),
                new Stats.SelfStatusDefinition(Stats.BuiltInStatuses.CreateEmpowerment()));
    }
}

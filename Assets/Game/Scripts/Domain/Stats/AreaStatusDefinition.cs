using System;
using Game.Domain.Abilities;

namespace Game.Domain.Stats {
    [Flags] public enum StatusTargets { Player = 1, Enemy = 2, Projectile = 4 }
    public sealed class AreaStatusDefinition : AbilityEffectDefinition {
        public float Radius { get; }
        public StatusTargets Targets { get; }
        public StatusEffectDefinition Status { get; }
        public AreaStatusDefinition(float radius, StatusTargets targets, StatusEffectDefinition status) {
            StatValidation.Finite(radius);
            if (radius <= 0f || targets == 0 || (targets & ~(StatusTargets.Player | StatusTargets.Enemy | StatusTargets.Projectile)) != 0)
                throw new ArgumentOutOfRangeException(nameof(radius));
            this.Radius = radius; this.Targets = targets; this.Status = status ?? throw new ArgumentNullException(nameof(status));
        }
        public override int GetDamageCapacity(int targetCapacity) => 0;
        public override bool UsesPeriodicDamage => this.Status.PeriodicDamage != null;
    }
}

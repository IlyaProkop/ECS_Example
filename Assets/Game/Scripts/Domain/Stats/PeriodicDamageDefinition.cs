using System;

namespace Game.Domain.Stats {
    // One tick at most per 60 Hz simulation step; no immediate damage on application.
    public sealed class PeriodicDamageDefinition {
        public float Interval { get; }
        public float Damage { get; }
        public DamageKind Kind { get; }
        public PeriodicDamageDefinition(float interval, float damage, DamageKind kind = DamageKind.Periodic) {
            StatValidation.Finite(interval); StatValidation.Finite(damage);
            if (interval < 1f / 60f || damage < 0f) throw new ArgumentOutOfRangeException(nameof(interval));
            this.Interval = interval; this.Damage = damage; this.Kind = kind;
        }
    }
}

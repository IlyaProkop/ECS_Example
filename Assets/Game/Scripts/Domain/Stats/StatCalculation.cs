using System;

namespace Game.Domain.Stats {
    // Rebuilt from the authoritative base and active sources, never an inverse subtraction.
    public struct StatCalculation {
        private readonly double baseValue;
        private double addition, multiplier;
        private bool zeroMultiplier, hasOverride;
        private float overrideValue;
        private int overridePriority, overrideOrdinal;
        private long overrideSequence;
        public StatCalculation(float baseValue) {
            this = default;
            this.baseValue = baseValue;
            this.multiplier = 1d;
        }
        public void Apply(in StatModifierDefinition modifier, long sequence, int ordinal) {
            switch (modifier.Operation) {
                case StatOperation.Add: this.addition += modifier.Value; break;
                case StatOperation.Multiply:
                    if (modifier.Value == 0f) this.zeroMultiplier = true;
                    else this.multiplier *= modifier.Value;
                    break;
                case StatOperation.Override:
                    if (!this.hasOverride || modifier.Priority > this.overridePriority ||
                        (modifier.Priority == this.overridePriority && (sequence > this.overrideSequence ||
                        (sequence == this.overrideSequence && ordinal > this.overrideOrdinal)))) {
                        this.hasOverride = true; this.overrideValue = modifier.Value;
                        this.overridePriority = modifier.Priority; this.overrideSequence = sequence; this.overrideOrdinal = ordinal;
                    }
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(modifier));
            }
        }
        public float Resolve(StatDefinition definition) {
            if (this.hasOverride) return definition.Clamp(this.overrideValue);
            var sum = this.baseValue + this.addition;
            // Even extreme finite factors cannot produce NaN via 0 * Infinity.
            return definition.Clamp(this.zeroMultiplier || sum == 0d ? 0d : sum * this.multiplier);
        }
    }
}

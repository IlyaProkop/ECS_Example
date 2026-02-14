using System;

namespace Game.Domain {
    public struct DamageCalculation {
        public float amount;
        public float armor;
        public float criticalChance;
        public float criticalMultiplier;
        public float criticalRoll;
        public DamageKind kind;
        public bool isCritical;
    }

    public interface IDamageModifier {
        void Apply(ref DamageCalculation damage);
    }

    public sealed class CriticalDamageModifier : IDamageModifier {
        public void Apply(ref DamageCalculation damage) {
            damage.isCritical = damage.criticalRoll < Math.Clamp(damage.criticalChance, 0f, 1f);
            if (damage.isCritical) {
                damage.amount *= Math.Max(1f, damage.criticalMultiplier);
            }
        }
    }

    public sealed class ArmorDamageModifier : IDamageModifier {
        public void Apply(ref DamageCalculation damage) {
            damage.amount *= 100f / (100f + Math.Max(0f, damage.armor));
        }
    }

    public sealed class DamagePipeline {
        private readonly IDamageModifier[] modifiers;

        public DamagePipeline(params IDamageModifier[] modifiers) {
            if (modifiers == null) throw new ArgumentNullException(nameof(modifiers));
            this.modifiers = (IDamageModifier[])modifiers.Clone();
            foreach (var modifier in this.modifiers) {
                if (modifier == null) throw new ArgumentException("A damage modifier cannot be null.", nameof(modifiers));
            }
        }

        public float Resolve(ref DamageCalculation damage, float currentHealth) {
            damage.isCritical = false;
            if (!IsFinite(damage.amount) || !IsFinite(currentHealth) || damage.amount <= 0f || currentHealth <= 0f) {
                return 0f;
            }
            for (var i = 0; i < this.modifiers.Length; i++) {
                this.modifiers[i].Apply(ref damage);
            }
            return IsFinite(damage.amount) ? Math.Clamp(damage.amount, 0f, currentHealth) : 0f;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

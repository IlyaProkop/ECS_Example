using System;
using System.Collections.ObjectModel;

namespace Game.Domain.Abilities {
    // Definitions are immutable. Executors and their scratch memory belong to one session.
    public sealed class AbilityDefinition {
        public int Id { get; }
        public float Cooldown { get; }
        public ReadOnlyCollection<AbilityEffectDefinition> Effects { get; }

        public AbilityDefinition(int id, float cooldown, params AbilityEffectDefinition[] effects) {
            if (id < 1) throw new ArgumentOutOfRangeException(nameof(id));
            if (float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cooldown));
            if (effects == null || effects.Length == 0 || effects.Length > 16) throw new ArgumentException("An ability supports 1 to 16 effects.", nameof(effects));
            var copy = (AbilityEffectDefinition[])effects.Clone();
            foreach (var effect in copy) if (effect == null) throw new ArgumentException("An effect cannot be null.", nameof(effects));
            this.Id = id;
            this.Cooldown = cooldown;
            this.Effects = Array.AsReadOnly(copy);
        }

        public int GetDamageCapacity(int targetCapacity) => this.GetDamageCapacity(new AbilityTargetBudget(targetCapacity, targetCapacity));
        public int GetDamageCapacity(AbilityTargetBudget targets) {
            var capacity = 0;
            foreach (var effect in this.Effects) {
                var count = effect.GetDamageCapacity(targets);
                if (count < 0) throw new InvalidOperationException("Effect capacity cannot be negative.");
                capacity = checked(capacity + count);
            }
            return capacity;
        }
    }

    public abstract class AbilityEffectDefinition {
        public virtual bool UsesPeriodicDamage => false;
        // Maximum damage requests from one cast, including every target and repeated hit.
        public abstract int GetDamageCapacity(int targetCapacity);
        public virtual int GetDamageCapacity(AbilityTargetBudget targets) => this.GetDamageCapacity(targets.AllActors);
    }
}

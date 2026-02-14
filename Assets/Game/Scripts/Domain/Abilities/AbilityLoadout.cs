using System;
using System.Collections.ObjectModel;

namespace Game.Domain.Abilities {
    // Explicit gameplay budget, shared by authoring, slot storage and input masks.
    public sealed class AbilityLoadout {
        public const int MaxSlots = 4;
        public ReadOnlyCollection<AbilityDefinition> Slots { get; }
        public AbilityLoadout(params AbilityDefinition[] slots) {
            if (slots == null || slots.Length > MaxSlots) throw new ArgumentException("An actor supports at most four abilities.");
            var copy = (AbilityDefinition[])slots.Clone();
            foreach (var slot in copy) if (slot == null) throw new ArgumentException("An ability slot cannot be null.");
            this.Slots = Array.AsReadOnly(copy);
        }
        public int DamageCapacity(int targets) => this.DamageCapacity(new AbilityTargetBudget(targets, targets));
        public int DamageCapacity(AbilityTargetBudget targets) {
            var result = 0;
            foreach (var slot in this.Slots) result = checked(result + slot.GetDamageCapacity(targets));
            return result;
        }
    }
}

using System;

namespace Game.Domain.Abilities {
    public interface IAbilityCommands { bool TryActivateAbility(int slot); }
    public readonly struct AbilitySlotSnapshot {
        public readonly int Id;
        public readonly float Cooldown, Remaining;
        public readonly bool CanActivate;
        public AbilitySlotSnapshot(int id, float cooldown, float remaining, bool canActivate) {
            this.Id = id; this.Cooldown = cooldown; this.Remaining = remaining; this.CanActivate = canActivate;
        }
    }
    // Inline value storage: no allocation per frame, no shared mutable collection.
    public readonly struct AbilityBarSnapshot {
        private readonly AbilitySlotSnapshot first, second, third, fourth;
        public readonly int Count;
        public AbilityBarSnapshot(int count, AbilitySlotSnapshot first, AbilitySlotSnapshot second = default,
            AbilitySlotSnapshot third = default, AbilitySlotSnapshot fourth = default) {
            if ((uint)count > AbilityLoadout.MaxSlots) throw new ArgumentOutOfRangeException(nameof(count));
            this.Count = count; this.first = first; this.second = second; this.third = third; this.fourth = fourth;
        }
        public AbilitySlotSnapshot this[int slot] => (uint)slot >= this.Count ? throw new ArgumentOutOfRangeException(nameof(slot))
            : slot switch { 0 => this.first, 1 => this.second, 2 => this.third, _ => this.fourth };
    }
}

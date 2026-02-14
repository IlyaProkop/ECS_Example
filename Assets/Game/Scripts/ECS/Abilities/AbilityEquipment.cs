using Game.Domain.Abilities;
using Game.ECS.Components;
using Scellecs.Morpeh;

namespace Game.ECS.Abilities {
    internal static class AbilityEquipment {
        public static void Initialize(World world, Entity owner, AbilityLoadout loadout) {
            if (loadout.Slots.Count == 0) return;
            var state = new AbilityComponent { count = loadout.Slots.Count };
            for (var i = 0; i < state.count; i++) state.Set(i, new AbilitySlot {
                definitionId = loadout.Slots[i].Id, cooldown = loadout.Slots[i].Cooldown
            });
            world.GetStash<AbilityComponent>().Set(owner, state);
        }
        // Player and AI producers use the same one-step intent. Duplicates coalesce.
        public static bool Request(World world, Entity owner, int slot) {
            if (owner.IsNullOrDisposed() || !world.TryGetEntity(owner.ID, out var own) || !ReferenceEquals(own, owner)) return false;
            var abilities = world.GetStash<AbilityComponent>();
            if (!abilities.Has(owner)) return false;
            ref var state = ref abilities.Get(owner);
            if ((uint)slot >= state.count) return false;
            state.requestedSlots |= 1u << slot;
            return true;
        }
    }
}

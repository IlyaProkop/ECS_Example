using Game.ECS.Components;
using Scellecs.Morpeh;

namespace Game.ECS.Weapons {
    // Initial slots determine the session budget. Runtime swaps cannot arm extra actors.
    internal static class WeaponEquipment {
        public static void Initialize(World world, Entity actor, WeaponCatalog catalog, int weaponId) {
            world.GetStash<WeaponComponent>().Set(actor, new WeaponComponent { definitionIndex = catalog.IndexOf(weaponId) });
            world.GetStash<AttackIntentComponent>().Add(actor);
        }
        public static bool TryChange(World world, Entity actor, WeaponCatalog catalog, int weaponId) {
            if (!catalog.TryIndexOf(weaponId, out var index) || actor.IsNullOrDisposed() ||
                !world.TryGetEntity(actor.ID, out var own) || !ReferenceEquals(own, actor)) return false;
            var weapons = world.GetStash<WeaponComponent>();
            var health = world.GetStash<HealthComponent>();
            if (!weapons.Has(actor) || !health.Has(actor) || health.Get(actor).current <= 0f || world.GetStash<DestroyTag>().Has(actor)) return false;
            var state = weapons.Get(actor);
            state.definitionIndex = index; // Remaining cooldown is conserved, including repeated swaps.
            weapons.Set(actor, state); return true;
        }
    }
}

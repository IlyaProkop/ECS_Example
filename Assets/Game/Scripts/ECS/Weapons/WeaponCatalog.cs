using Game.Domain;
using Game.ECS.Projectiles;

namespace Game.ECS.Weapons {
    internal readonly struct CompiledWeapon {
        public readonly WeaponDefinition Definition;
        public readonly int ProjectileIndex;
        public CompiledWeapon(WeaponDefinition definition, int projectileIndex) { this.Definition = definition; this.ProjectileIndex = projectileIndex; }
    }
    internal sealed class WeaponCatalog {
        private readonly WeaponSet definitions;
        private readonly CompiledWeapon[] weapons;
        public WeaponCatalog(WeaponSet definitions, ProjectileRuntimeCatalog projectiles) {
            this.definitions = definitions; this.weapons = new CompiledWeapon[definitions.Definitions.Count];
            for (var i = 0; i < this.weapons.Length; i++) {
                var weapon = definitions.Definitions[i];
                this.weapons[i] = new CompiledWeapon(weapon, projectiles.Compile(weapon.Projectile));
            }
        }
        public CompiledWeapon Get(int index) => this.weapons[index];
        public int IndexOf(int id) => this.definitions.IndexOf(id);
        public bool TryIndexOf(int id, out int index) => this.definitions.TryIndexOf(id, out index);
    }
}

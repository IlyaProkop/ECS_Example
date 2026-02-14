using System;
using System.Collections.Generic;
using Game.Domain;

namespace Game.Config {
    // One authoring conversion scope: shared assets become shared immutable definitions.
    internal sealed class WeaponAuthoringCache {
        private readonly Dictionary<WeaponAsset, WeaponDefinition> weapons = new();
        private readonly Dictionary<ProjectileAsset, ProjectileDefinition> projectiles = new();
        public WeaponDefinition Resolve(WeaponAsset asset) {
            if (asset == null) return null;
            if (this.weapons.TryGetValue(asset, out var weapon)) return weapon;
            if (asset.projectile == null) throw new InvalidOperationException("Weapon requires a projectile.");
            if (!this.projectiles.TryGetValue(asset.projectile, out var projectile)) {
                projectile = asset.projectile.CreateDefinition(); this.projectiles.Add(asset.projectile, projectile);
            }
            weapon = asset.CreateDefinition(projectile); this.weapons.Add(asset, weapon); return weapon;
        }
    }
}

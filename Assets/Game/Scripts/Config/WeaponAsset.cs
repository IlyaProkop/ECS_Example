using Game.Domain;
using UnityEngine;

namespace Game.Config {
    [CreateAssetMenu(menuName = "Game/Combat/Weapon", fileName = "Weapon")]
    public sealed class WeaponAsset : ScriptableObject {
        [Min(1)] public int id = 1;
        [Min(.001f)] public float interval = .3f;
        [Min(.001f)] public float range = 1000f;
        public ProjectileAsset projectile;
        public WeaponDefinition CreateDefinition() => this.CreateDefinition(this.projectile != null
            ? this.projectile.CreateDefinition() : throw new System.InvalidOperationException("Weapon requires a projectile."));
        internal WeaponDefinition CreateDefinition(ProjectileDefinition projectile) => new WeaponDefinition(this.interval, projectile, this.id, this.range);
    }
}

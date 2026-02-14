using Game.Domain;
using UnityEngine;

namespace Game.Config {
    [CreateAssetMenu(menuName = "Game/Combat/Hit/Direct damage", fileName = "DirectDamage")]
    public sealed class DirectDamageHitAsset : ProjectileHitAsset {
        public DamageKind kind = DamageKind.Projectile;
        public override ProjectileHitDefinition CreateDefinition() => new DirectProjectileDamage(this.kind);
    }
}

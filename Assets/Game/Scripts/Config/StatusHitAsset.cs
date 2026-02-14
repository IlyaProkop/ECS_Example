using Game.Domain;
using UnityEngine;

namespace Game.Config {
    [CreateAssetMenu(menuName = "Game/Combat/Hit/Apply status", fileName = "StatusHit")]
    public sealed class StatusHitAsset : ProjectileHitAsset {
        public StatusEffectAsset status;
        public override ProjectileHitDefinition CreateDefinition() => new ProjectileStatusHit(this.status != null
            ? this.status.CreateDefinition() : throw new System.InvalidOperationException("Hit effect requires a status."));
    }
}

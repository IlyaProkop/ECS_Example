using Game.Domain;
using UnityEngine;

namespace Game.Config {
    public abstract class ProjectileHitAsset : ScriptableObject {
        public abstract ProjectileHitDefinition CreateDefinition();
    }
}

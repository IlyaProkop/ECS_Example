using UnityEngine;

namespace Game.UI {
    internal readonly struct EnemyHealthLabel {
        public readonly int ActorId;
        public readonly Vector3 ScreenPosition;
        public readonly float Health;
        public EnemyHealthLabel(int actorId, Vector3 screenPosition, float health) {
            this.ActorId = actorId; this.ScreenPosition = screenPosition; this.Health = health;
        }
    }
}

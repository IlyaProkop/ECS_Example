using UnityEngine;

namespace Game.Domain {
    public enum AttackAimKind { NearestHostile, Actor, Direction }
    // Actor IDs are monotonic and local to the GameSession that produced them.
    public struct AttackAim {
        public AttackAimKind kind;
        public int actorId;
        public Vector2 direction;
    }
}

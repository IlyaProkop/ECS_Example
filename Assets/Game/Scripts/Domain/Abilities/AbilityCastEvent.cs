using UnityEngine;

namespace Game.Domain.Abilities {
    public readonly struct AbilityCastEvent {
        public readonly long sequence;
        public readonly int abilityId;
        public readonly Vector3 position;
        public AbilityCastEvent(long sequence, int abilityId, Vector3 position) {
            this.sequence = sequence;
            this.abilityId = abilityId;
            this.position = position;
        }
    }
}

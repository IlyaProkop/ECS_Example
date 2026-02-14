using Game.Domain;
using UnityEngine;

namespace Game.Spatial {
    internal static class ArenaGeometry {
        public const float DoorHalfWidth = 2f;

        public static bool IsInside(SimulationSettings config, Vector3 position, float radius) {
            return Mathf.Abs(position.x) <= config.arenaHalfSize.x - radius &&
                   Mathf.Abs(position.z) <= config.arenaHalfSize.y - radius;
        }

    }
}

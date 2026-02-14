using UnityEngine;

namespace Game.Domain {
    public sealed class NavigationSettings {
        public readonly float enemyPathCellSize, enemyPathRepathInterval, enemyPathRepathJitter,
            enemyPathWaypointReachDistance, enemyPathTargetMoveThreshold, enemyPathSearchPadding, enemyPathMaxSearchSize;
        public readonly int enemyPathMaxNodes, enemyPathMaxRepathsPerFrame;
        internal NavigationSettings(SimulationSettings source) {
            this.enemyPathCellSize = source.enemyPathCellSize;
            this.enemyPathRepathInterval = source.enemyPathRepathInterval;
            this.enemyPathRepathJitter = source.enemyPathRepathJitter;
            this.enemyPathWaypointReachDistance = source.enemyPathWaypointReachDistance;
            this.enemyPathTargetMoveThreshold = source.enemyPathTargetMoveThreshold;
            this.enemyPathSearchPadding = source.enemyPathSearchPadding;
            this.enemyPathMaxSearchSize = source.enemyPathMaxSearchSize;
            this.enemyPathMaxNodes = source.enemyPathMaxNodes;
            this.enemyPathMaxRepathsPerFrame = source.enemyPathMaxRepathsPerFrame;
        }
    }
    public sealed class ArenaSettings {
        public readonly Vector2 HalfSize;
        public readonly Vector3 ObstacleScale;
        internal ArenaSettings(SimulationSettings source) { this.HalfSize = source.arenaHalfSize; this.ObstacleScale = source.obstacleScale; }
    }
}

using UnityEngine;

namespace Game.Spatial {
    // Prepared per actor/radius, borrowed from the session's immutable obstacle index.
    // Reusable across velocity movement and the subsequent separation correction.
    internal readonly struct ActorMotionQuery {
        // The four-obstacle arena is cheaper to scan than to prepare/traverse grid cells.
        private const int LinearScanLimit = 4;
        private readonly ObstacleGridLookupNative obstacles;
        private readonly int obstacleCount;
        private readonly float radius, maxStep, halfX, halfZ;
        private readonly float minX, maxX, minZ, maxZ, entryZ, doorMinX, doorMaxX;
        private readonly bool closed;

        public ActorMotionQuery(Vector2 arenaHalfSize, float radius, bool closed, in ObstacleGridLookupNative obstacles) {
            this.obstacles = obstacles;
            this.obstacleCount = obstacles.obstacleBounds.IsCreated ? obstacles.obstacleBounds.Length : 0;
            this.radius = radius;
            this.maxStep = Mathf.Max(0.05f, radius * 0.5f);
            this.halfX = obstacles.obstacleHalfX + radius;
            this.halfZ = obstacles.obstacleHalfZ + radius;
            this.minX = -arenaHalfSize.x + radius;
            this.maxX = arenaHalfSize.x - radius;
            this.entryZ = -arenaHalfSize.y + radius;
            this.minZ = closed ? this.entryZ : -arenaHalfSize.y - 4f;
            this.maxZ = arenaHalfSize.y - radius;
            this.doorMinX = -ArenaGeometry.DoorHalfWidth + radius;
            this.doorMaxX = ArenaGeometry.DoorHalfWidth - radius;
            this.closed = closed;
        }

        public bool IsBlocked(Vector3 position) => this.obstacleCount <= LinearScanLimit
            ? this.IsBlockedLinear(position)
            : this.IsBlocked(position, ObstacleQueries.QueryBounds(this.obstacles,
                position.x, position.x, position.z, position.z, this.radius));

        public Vector3 Move(Vector3 position, Vector3 displacement) {
            var steps = Mathf.Max(1, Mathf.CeilToInt(MotionMath.Length(displacement) / this.maxStep));
            var step = displacement / steps;
            var candidates = this.obstacleCount <= LinearScanLimit ? default : this.MovementCandidates(position, displacement);
            // Preserve X-then-Z sliding and the old subdivision arithmetic. Reuse the same
            // candidate set for every axis/substep, without collecting IDs into a scratch list.
            for (var i = 0; i < steps; i++) {
                var next = this.Clamp(position + new Vector3(step.x, 0f, 0f));
                if (!this.IsBlocked(next, candidates)) position = next;
                next = this.Clamp(position + new Vector3(0f, 0f, step.z));
                if (!this.IsBlocked(next, candidates)) position = next;
            }
            return position;
        }

        private Vector3 Clamp(Vector3 position) {
            position.x = Mathf.Clamp(position.x, this.minX, this.maxX);
            position.z = Mathf.Clamp(position.z, this.minZ, this.maxZ);
            if (!this.closed && position.z < this.entryZ)
                position.x = Mathf.Clamp(position.x, this.doorMinX, this.doorMaxX);
            position.y = 0f;
            return position;
        }

        private bool IsBlocked(Vector3 position, in ObstacleQueries.BoundsQuery candidates) {
            foreach (var index in candidates) {
                if (this.Overlaps(position, index)) return true;
            }
            return false;
        }

        private bool IsBlockedLinear(Vector3 position) {
            for (var i = 0; i < this.obstacleCount; i++)
                if (this.Overlaps(position, i)) return true;
            return false;
        }

        private bool Overlaps(Vector3 position, int index) {
            var center = this.obstacles.obstacleBounds[index].center;
            // Movement permits tangency. Segment casts deliberately use inclusive bounds.
            return Mathf.Abs(position.x - center.x) < this.halfX && Mathf.Abs(position.z - center.y) < this.halfZ;
        }

        private bool IsBlocked(Vector3 position, in MotionCandidates candidates) {
            if (this.obstacleCount <= LinearScanLimit) return this.IsBlockedLinear(position);
            return candidates.Covers(position) ? this.IsBlocked(position, candidates.Query) : this.IsBlocked(position);
        }

        private MotionCandidates MovementCandidates(Vector3 start, Vector3 displacement) {
            var projectedX = Mathf.Clamp(start.x, this.minX, this.maxX);
            var projectedZ = Mathf.Clamp(start.z, this.minZ, this.maxZ);
            var lowX = Mathf.Min(start.x, projectedX);
            var highX = Mathf.Max(start.x, projectedX);
            if (!this.closed) {
                // Gate clamping can move X even when displacement.x is zero. Include this
                // anchor before expanding by travel; it also covers starts outside the arena.
                var gateX = Mathf.Clamp(projectedX, this.doorMinX, this.doorMaxX);
                lowX = Mathf.Min(lowX, gateX);
                highX = Mathf.Max(highX, gateX);
            }
            // Repeated float additions can slightly exceed the mathematical endpoint.
            // A lookup-cell halo keeps ordinary motion in this reusable query. If a clamp or
            // accumulated rounding leaves the predicted area, IsBlocked falls back to a point query.
            var travelX = Mathf.Abs(displacement.x) + this.obstacles.cellSize;
            var travelZ = Mathf.Abs(displacement.z) + this.obstacles.cellSize;
            return new MotionCandidates(this.obstacles, lowX - travelX, highX + travelX,
                Mathf.Min(start.z, projectedZ) - travelZ, Mathf.Max(start.z, projectedZ) + travelZ, this.radius);
        }

        private readonly struct MotionCandidates {
            public readonly ObstacleQueries.BoundsQuery Query;
            private readonly float minX, maxX, minZ, maxZ;
            public MotionCandidates(in ObstacleGridLookupNative lookup, float minX, float maxX, float minZ, float maxZ, float radius) {
                this.minX = minX; this.maxX = maxX; this.minZ = minZ; this.maxZ = maxZ;
                this.Query = ObstacleQueries.QueryBounds(lookup, minX, maxX, minZ, maxZ, radius);
            }
            public bool Covers(Vector3 point) => point.x >= this.minX && point.x <= this.maxX && point.z >= this.minZ && point.z <= this.maxZ;
        }
    }
}

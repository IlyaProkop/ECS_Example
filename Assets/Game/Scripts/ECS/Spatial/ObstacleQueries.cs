using System;
using UnityEngine;

namespace Game.Spatial {
    // Shared broad phase over the immutable session index. Candidates are obstacle IDs,
    // without duplicates; each caller retains its own exact geometric test.
    internal static class ObstacleQueries {
        public static BoundsQuery QueryBounds(in ObstacleGridLookupNative lookup,
            float minX, float maxX, float minZ, float maxZ, float margin) =>
            new BoundsQuery(lookup, minX, maxX, minZ, maxZ, margin);

        public static bool HasLineOfSight(Vector3 start, Vector3 end, float radius, in ObstacleGridLookupNative lookup) {
            var from = new Vector2(start.x, start.z);
            var to = new Vector2(end.x, end.z);
            foreach (var index in QuerySegment(from, to, radius, lookup)) {
                var obstacle = lookup.obstacleBounds[index];
                if (Geometry2D.SegmentIntersectsAabb2D(from, to,
                    obstacle.minX - radius, obstacle.maxX + radius, obstacle.minZ - radius, obstacle.maxZ + radius)) return false;
            }
            return true;
        }

        public static bool TryFindFirstHit(Vector2 start, Vector2 end, float radius,
            in ObstacleGridLookupNative lookup, out float hitT) {
            hitT = float.MaxValue;
            foreach (var index in QuerySegment(start, end, radius, lookup)) {
                var obstacle = lookup.obstacleBounds[index];
                if (Geometry2D.SegmentIntersectsAabb2D(start, end,
                    obstacle.minX - radius, obstacle.maxX + radius, obstacle.minZ - radius, obstacle.maxZ + radius, out var t)
                    && t < hitT) hitT = t;
            }
            return hitT < float.MaxValue;
        }

        private static BoundsQuery QuerySegment(Vector2 start, Vector2 end, float radius, in ObstacleGridLookupNative lookup) =>
            QueryBounds(lookup, Mathf.Min(start.x, end.x), Mathf.Max(start.x, end.x),
                Mathf.Min(start.y, end.y), Mathf.Max(start.y, end.y), radius);

        // Borrowed NativeArrays: consume before the owning ObstacleGridLookup is disposed.
        // Grid order is Z, then X, then authoring order within the cell. Large rectangles
        // use a linear scan in authoring order; no consumer relies on equal-distance obstacle IDs.
        internal readonly struct BoundsQuery {
            private readonly ObstacleGridLookupNative lookup;
            private readonly int minX, maxX, minZ, maxZ, count;
            private readonly bool linear;

            internal BoundsQuery(in ObstacleGridLookupNative lookup, float minX, float maxX, float minZ, float maxZ, float margin) {
                this = default;
                this.lookup = lookup;
                this.count = lookup.obstacleBounds.IsCreated ? lookup.obstacleBounds.Length : 0;
                if (this.count == 0) return;
                var halfX = lookup.obstacleHalfX + margin;
                var halfZ = lookup.obstacleHalfZ + margin;
                this.minX = Mathf.Clamp(Mathf.FloorToInt((minX - halfX - lookup.originX) * lookup.inverseCellSize), 0, lookup.width - 1);
                this.maxX = Mathf.Clamp(Mathf.FloorToInt((maxX + halfX - lookup.originX) * lookup.inverseCellSize), 0, lookup.width - 1);
                this.minZ = Mathf.Clamp(Mathf.FloorToInt((minZ - halfZ - lookup.originZ) * lookup.inverseCellSize), 0, lookup.height - 1);
                this.maxZ = Mathf.Clamp(Mathf.FloorToInt((maxZ + halfZ - lookup.originZ) * lookup.inverseCellSize), 0, lookup.height - 1);
                this.linear = ((long)this.maxX - this.minX + 1) * ((long)this.maxZ - this.minZ + 1) > this.count;
            }

            public Enumerator GetEnumerator() => new Enumerator(this.lookup,
                this.minX, this.maxX, this.minZ, this.maxZ, this.count, this.linear);
        }

        internal struct Enumerator {
            private readonly ObstacleGridLookupNative lookup;
            private readonly int minX, maxX, maxZ, count;
            private readonly bool linear;
            private int x, z, next, end, current;
            private bool cellsRemaining, hasCurrent;

            internal Enumerator(in ObstacleGridLookupNative lookup, int minX, int maxX, int minZ, int maxZ, int count, bool linear) {
                this = default;
                this.lookup = lookup; this.minX = minX; this.maxX = maxX; this.maxZ = maxZ;
                this.count = count; this.linear = linear;
                this.x = minX; this.z = minZ;
                this.cellsRemaining = count > 0 && minX <= maxX && minZ <= maxZ;
            }

            public int Current => this.hasCurrent ? this.current : throw new InvalidOperationException("Obstacle cursor has no current item.");

            public bool MoveNext() {
                this.hasCurrent = false;
                if (this.linear) {
                    if (this.next == this.count) return false;
                    this.current = this.next++;
                    return this.hasCurrent = true;
                }
                while (this.next == this.end) {
                    if (!this.cellsRemaining) return false;
                    var cell = this.z * this.lookup.width + this.x;
                    this.next = this.lookup.cellStarts[cell];
                    this.end = this.next + this.lookup.cellCounts[cell];
                    if (this.x == this.maxX) {
                        this.x = this.minX;
                        if (this.z == this.maxZ) this.cellsRemaining = false;
                        else this.z++;
                    } else this.x++;
                }
                this.current = this.lookup.obstacleIndices[this.next++];
                return this.hasCurrent = true;
            }
        }
    }
}

using System;
using UnityEngine;

namespace Game.Spatial {
    internal interface IEnemyGeometryEnumerator {
        bool MoveNext();
        EnemyGeometrySample Current { get; }
    }

    // Contiguous geometry only: neighbor scans never touch a Morpeh entity or component.
    internal readonly struct EnemyGeometrySample {
        public readonly int ActorId;
        public readonly Vector2 Position;
        public readonly float Radius;

        public EnemyGeometrySample(int actorId, Vector3 position, float radius) {
            this.ActorId = actorId;
            this.Position = new Vector2(position.x, position.z);
            this.Radius = radius;
        }
    }

    // A bounded, single-threaded snapshot. The owner controls writes; consumers borrow a ReadView.
    internal sealed class EnemyGeometryIndex {
        private const float CellSize = 2f;
        private readonly EnemyGeometrySample[] samples;
        private readonly bool[] disabled;
        private readonly int[] next;
        private readonly SpatialCellHeads heads;
        private long version;
        public int Count { get; private set; }
        public int Capacity => this.samples.Length;

        public EnemyGeometryIndex(int capacity) {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.heads = new SpatialCellHeads(capacity);
            this.samples = new EnemyGeometrySample[capacity];
            this.disabled = new bool[capacity];
            this.next = new int[capacity];
        }

        public void BeginFrame() {
            this.version++;
            this.Count = 0;
            this.heads.Clear();
            // Value-only storage needs no clearing. Add overwrites each published slot.
        }

        public void Add(in EnemyGeometrySample sample) {
            if (this.Count == this.Capacity) throw new InvalidOperationException("Enemy spatial capacity exceeded.");
            var index = this.Count;
            this.next[index] = this.heads.Replace(Cell(sample.Position), index);
            this.samples[index] = sample;
            this.disabled[index] = false;
            this.Count++;
            this.version++;
        }

        // Keep the slot and bucket order: removing a stale handle must not reorder live neighbors.
        public void Disable(int index) {
            this.ValidateIndex(index);
            if (this.disabled[index]) return;
            this.disabled[index] = true;
            this.version++;
        }

        public EnemyGeometrySample SampleAt(int index) {
            this.ValidateIndex(index);
            return this.samples[index];
        }

        public ReadView Borrow() => new ReadView(this, this.version);

        private void ValidateIndex(int index) {
            if ((uint)index >= this.Count) throw new ArgumentOutOfRangeException(nameof(index));
        }

        private static Vector2Int Cell(Vector2 point) => new Vector2Int(Mathf.FloorToInt(point.x / CellSize), Mathf.FloorToInt(point.y / CellSize));

        internal readonly struct ReadView {
            private readonly EnemyGeometryIndex owner;
            private readonly long version;

            internal ReadView(EnemyGeometryIndex owner, long version) {
                this.owner = owner;
                this.version = version;
            }

            public void CopyTo(NativeEnemyGeometrySnapshot destination) {
                Validate(this.owner, this.version);
                destination.BeginFrame();
                for (var i = 0; i < this.owner.Count; i++)
                    destination.Add(this.owner.samples[i], this.owner.disabled[i]);
            }

            public CircleQuery QueryCircle(Vector3 position, float radius) {
                Validate(this.owner, this.version);
                if (radius < 0f || float.IsNaN(radius) || float.IsInfinity(radius)) throw new ArgumentOutOfRangeException(nameof(radius));
                return new CircleQuery(this.owner, this.version, new Vector2(position.x, position.z), radius);
            }
        }

        // Pattern-based foreach: no iterator object, interface dispatch, boxing or result list.
        internal readonly struct CircleQuery {
            private readonly EnemyGeometryIndex owner;
            private readonly long version;
            private readonly Vector2 center;
            private readonly float radius;
            internal CircleQuery(EnemyGeometryIndex owner, long version, Vector2 center, float radius) {
                this.owner = owner; this.version = version; this.center = center; this.radius = radius;
            }
            public Enumerator GetEnumerator() => new Enumerator(this.owner, this.version, this.center, this.radius);
        }

        internal struct Enumerator : IEnemyGeometryEnumerator {
            private readonly EnemyGeometryIndex owner;
            private readonly long version;
            private readonly Vector2 center;
            private readonly float squaredRadius;
            private readonly int minX, maxX, maxY;
            private readonly bool linear;
            private int x, y, nextIndex, currentIndex;
            private bool cellsRemaining;

            internal Enumerator(EnemyGeometryIndex owner, long version, Vector2 center, float radius) {
                this = default;
                Validate(owner, version);
                this.owner = owner; this.version = version; this.center = center;
                this.squaredRadius = radius * radius;
                var min = Cell(center - Vector2.one * radius);
                var max = Cell(center + Vector2.one * radius);
                this.minX = min.x; this.maxX = max.x; this.maxY = max.y;
                this.x = min.x; this.y = min.y;
                this.linear = ((double)max.x - min.x + 1) * ((double)max.y - min.y + 1) > owner.Count;
                this.nextIndex = this.linear ? 0 : -1;
                this.currentIndex = -1;
                this.cellsRemaining = owner.Count > 0;
            }

            public int CurrentIndex {
                get {
                    Validate(this.owner, this.version);
                    if (this.currentIndex < 0) throw new InvalidOperationException("Spatial cursor has no current item.");
                    return this.currentIndex;
                }
            }
            public EnemyGeometrySample Current {
                get {
                    var index = this.CurrentIndex;
                    return this.owner.samples[index];
                }
            }

            public bool MoveNext() {
                Validate(this.owner, this.version);
                this.currentIndex = -1;
                if (this.linear) {
                    while (this.nextIndex < this.owner.Count) {
                        var index = this.nextIndex++;
                        if (this.Accept(index)) return true;
                    }
                    return false;
                }
                while (true) {
                    while (this.nextIndex >= 0) {
                        var index = this.nextIndex;
                        this.nextIndex = this.owner.next[index];
                        if (this.Accept(index)) return true;
                    }
                    if (!this.cellsRemaining) return false;
                    var cell = new Vector2Int(this.x, this.y);
                    if (this.x == this.maxX) {
                        this.x = this.minX;
                        if (this.y == this.maxY) this.cellsRemaining = false;
                        else this.y++;
                    } else this.x++;
                    this.nextIndex = this.owner.heads.Find(cell);
                }
            }

            private bool Accept(int index) {
                if (this.owner.disabled[index] || (this.owner.samples[index].Position - this.center).sqrMagnitude > this.squaredRadius) return false;
                this.currentIndex = index;
                return true;
            }
        }

        private static void Validate(EnemyGeometryIndex owner, long version) {
            if (owner == null || owner.version != version)
                throw new InvalidOperationException("Spatial query outlived its snapshot. Query again after rebuilding the index.");
        }
    }
}

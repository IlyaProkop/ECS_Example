using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Spatial {
    // Owned by movement. Built before scheduling, read only until the job completes.
    // Hash buckets preserve the managed snapshot's cell order and reverse insertion order.
    internal sealed class NativeEnemyGeometrySnapshot : IDisposable {
        private NativeArray<EnemyGeometrySample> samples;
        private NativeArray<byte> disabled;
        private NativeArray<int> next;
        private NativeParallelHashMap<int2, int> heads;
        private int count;
        public NativeEnemyGeometrySnapshot(int capacity) {
            try {
                this.samples = new NativeArray<EnemyGeometrySample>(capacity, Allocator.Persistent);
                this.disabled = new NativeArray<byte>(capacity, Allocator.Persistent);
                this.next = new NativeArray<int>(capacity, Allocator.Persistent);
                this.heads = new NativeParallelHashMap<int2, int>(Math.Max(1, capacity), Allocator.Persistent);
            } catch { this.Dispose(); throw; }
        }
        public void BeginFrame() { this.count = 0; this.heads.Clear(); }
        public void Add(in EnemyGeometrySample sample, bool disabled) {
            if (this.count == this.samples.Length) throw new InvalidOperationException("Native geometry capacity exceeded.");
            var cell = Cell(sample.Position);
            this.next[this.count] = this.heads.TryGetValue(cell, out var previous) ? previous : -1;
            this.heads[cell] = this.count;
            this.samples[this.count] = sample;
            this.disabled[this.count++] = disabled ? (byte)1 : (byte)0;
        }
        public ReadView Borrow() => new ReadView {
            Samples = this.samples, Disabled = this.disabled, Next = this.next,
            Heads = this.heads.AsReadOnly(), Count = this.count
        };
        public void Dispose() {
            if (this.heads.IsCreated) this.heads.Dispose();
            if (this.next.IsCreated) this.next.Dispose();
            if (this.disabled.IsCreated) this.disabled.Dispose();
            if (this.samples.IsCreated) this.samples.Dispose();
        }
        private static int2 Cell(Vector2 point) => new int2(Mathf.FloorToInt(point.x / 2f), Mathf.FloorToInt(point.y / 2f));

        internal struct ReadView {
            [ReadOnly] internal NativeArray<EnemyGeometrySample> Samples;
            [ReadOnly] internal NativeArray<byte> Disabled;
            [ReadOnly] internal NativeArray<int> Next;
            [ReadOnly] internal NativeParallelHashMap<int2, int>.ReadOnly Heads;
            internal int Count;
            public Enumerator Query(Vector3 position, float radius) => new Enumerator(this, new Vector2(position.x, position.z), radius);
        }
        internal struct Enumerator : IEnemyGeometryEnumerator {
            private ReadView view;
            private Vector2 center;
            private float squaredRadius;
            private int minX, maxX, maxY, x, y, nextIndex, currentIndex;
            private bool linear, cellsRemaining;
            internal Enumerator(ReadView view, Vector2 center, float radius) {
                this = default;
                this.view = view; this.center = center; this.squaredRadius = radius * radius;
                var min = Cell(center - Vector2.one * radius);
                var max = Cell(center + Vector2.one * radius);
                this.minX = min.x; this.maxX = max.x; this.maxY = max.y;
                this.x = min.x; this.y = min.y;
                this.linear = ((double)max.x - min.x + 1) * ((double)max.y - min.y + 1) > view.Count;
                this.nextIndex = this.linear ? 0 : -1;
                this.currentIndex = -1; this.cellsRemaining = view.Count > 0;
            }
            public EnemyGeometrySample Current => this.view.Samples[this.currentIndex];
            public bool MoveNext() {
                if (this.linear) {
                    while (this.nextIndex < this.view.Count) if (this.Accept(this.nextIndex++)) return true;
                    return false;
                }
                while (true) {
                    while (this.nextIndex >= 0) {
                        var index = this.nextIndex;
                        this.nextIndex = this.view.Next[index];
                        if (this.Accept(index)) return true;
                    }
                    if (!this.cellsRemaining) return false;
                    var cell = new int2(this.x, this.y);
                    if (this.x == this.maxX) {
                        this.x = this.minX;
                        if (this.y == this.maxY) this.cellsRemaining = false;
                        else this.y++;
                    } else this.x++;
                    this.nextIndex = this.view.Heads.TryGetValue(cell, out var head) ? head : -1;
                }
            }
            private bool Accept(int index) {
                if (this.view.Disabled[index] != 0 || MotionMath.SquaredLength((Unity.Mathematics.float2)(this.view.Samples[index].Position - this.center)) > this.squaredRadius) return false;
                this.currentIndex = index;
                return true;
            }
        }
    }
}

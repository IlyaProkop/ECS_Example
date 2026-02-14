using System;

namespace Game.ECS.Navigation {
    // Indexed min-heap for dense grid node IDs. One slot per node; decrease-key retains
    // insertion order so equal-cost choices match the previous stable linear search.
    internal sealed class PathOpenSet {
        private readonly int[] heap, positions, order;
        private readonly float[] costs;
        private int sequence;
        public int Count { get; private set; }

        public PathOpenSet(int capacity) {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.heap = new int[capacity];
            this.positions = new int[capacity]; // zero = absent, otherwise heap offset + 1
            this.order = new int[capacity];
            this.costs = new float[capacity];
        }
        public void Clear() {
            for (var i = 0; i < this.Count; i++) this.positions[this.heap[i]] = 0;
            this.Count = 0;
            this.sequence = 0;
        }
        public void AddOrDecrease(int node, float cost) {
            if ((uint)node >= this.positions.Length) throw new ArgumentOutOfRangeException(nameof(node));
            if (float.IsNaN(cost) || float.IsInfinity(cost)) throw new ArgumentOutOfRangeException(nameof(cost));
            var offset = this.positions[node] - 1;
            if (offset >= 0) {
                if (cost >= this.costs[node]) return;
            } else {
                offset = this.Count++;
                this.order[node] = this.sequence++;
            }
            this.costs[node] = cost;
            while (offset > 0) {
                var parent = (offset - 1) / 2;
                if (!this.Less(node, this.heap[parent])) break;
                this.Place(offset, this.heap[parent]);
                offset = parent;
            }
            this.Place(offset, node);
        }
        public int Pop() {
            if (this.Count == 0) throw new InvalidOperationException("Path open set is empty.");
            var node = this.heap[0];
            var last = this.heap[--this.Count];
            this.positions[node] = 0;
            if (this.Count == 0) return node;
            var offset = 0;
            while (true) {
                var child = offset * 2 + 1;
                if (child >= this.Count) break;
                if (child + 1 < this.Count && this.Less(this.heap[child + 1], this.heap[child])) child++;
                if (!this.Less(this.heap[child], last)) break;
                this.Place(offset, this.heap[child]);
                offset = child;
            }
            this.Place(offset, last);
            return node;
        }
        private bool Less(int left, int right) => this.costs[left] < this.costs[right] ||
            (this.costs[left] == this.costs[right] && this.order[left] < this.order[right]);
        private void Place(int offset, int node) {
            this.heap[offset] = node;
            this.positions[node] = offset + 1;
        }
    }
}

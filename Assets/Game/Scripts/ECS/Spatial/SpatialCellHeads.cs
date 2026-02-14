using System;
using UnityEngine;

namespace Game.Spatial {
    // At most one cell per actor, <= 50% load. Replace finds the old head and writes the new
    // one in a single probe sequence. Hash placement never determines query iteration order.
    internal sealed class SpatialCellHeads {
        private readonly Vector2Int[] keys;
        private readonly int[] heads;
        private readonly uint[] stamps;
        private readonly int mask;
        private readonly int capacity;
        private int count;
        private uint generation = 1;

        public SpatialCellHeads(int capacity) {
            if (capacity < 0 || capacity > 1 << 29) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
            var size = 2;
            while (size < capacity * 2) size <<= 1;
            this.keys = new Vector2Int[size];
            this.heads = new int[size];
            this.stamps = new uint[size];
            this.mask = size - 1;
        }

        public void Clear() {
            this.count = 0;
            this.generation = unchecked(this.generation + 1);
            if (this.generation != 0) return;
            Array.Clear(this.stamps, 0, this.stamps.Length);
            this.generation = 1;
        }

        public int Replace(Vector2Int cell, int head) {
            var slot = Hash(cell) & this.mask;
            while (this.stamps[slot] == this.generation) {
                if (this.keys[slot] == cell) {
                    var previous = this.heads[slot];
                    this.heads[slot] = head;
                    return previous;
                }
                slot = (slot + 1) & this.mask;
            }
            if (this.count == this.capacity) throw new InvalidOperationException("Spatial cell capacity exceeded.");
            this.count++;
            this.keys[slot] = cell;
            this.heads[slot] = head;
            this.stamps[slot] = this.generation;
            return -1;
        }

        public int Find(Vector2Int cell) {
            var slot = Hash(cell) & this.mask;
            while (this.stamps[slot] == this.generation) {
                if (this.keys[slot] == cell) return this.heads[slot];
                slot = (slot + 1) & this.mask;
            }
            return -1;
        }

        private static int Hash(Vector2Int cell) {
            // Mix both signed coordinates before masking; adjacent rows must not share clusters.
            unchecked {
                var hash = (uint)cell.x * 0x9e3779b1u ^ (uint)cell.y * 0x85ebca77u;
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                return (int)(hash ^ (hash >> 15));
            }
        }
    }
}

using System;
using System.Collections.Generic;

namespace Game.UI {
    // Reconcile the whole desired set before assigning new IDs. Stable IDs retain their slot;
    // a complete population turnover uses exactly Capacity slots, without temporary growth.
    internal sealed class HealthLabelSlots {
        private readonly int[] actors, items, free;
        private readonly Dictionary<int, int> requested;
        public int Capacity => this.actors.Length;
        public HealthLabelSlots(int capacity) {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.actors = new int[capacity]; this.items = new int[capacity]; this.free = new int[capacity];
            this.requested = new Dictionary<int, int>(capacity);
            this.Clear();
        }
        public int ItemAt(int slot) => this.items[slot];
        public void Bind(ReadOnlySpan<EnemyHealthLabel> labels) {
            if (labels.Length > this.Capacity) throw new ArgumentException("HP label capacity exceeded.", nameof(labels));
            this.requested.Clear();
            // Validate the complete input before changing any published assignment.
            for (var i = 0; i < labels.Length; i++) {
                if (labels[i].ActorId <= 0) throw new ArgumentException("HP labels require positive actor IDs.", nameof(labels));
                this.requested.Add(labels[i].ActorId, i);
            }
            var freeCount = 0;
            for (var slot = this.Capacity - 1; slot >= 0; slot--) {
                var id = this.actors[slot];
                if (id != 0 && this.requested.TryGetValue(id, out var item)) {
                    this.items[slot] = item;
                    this.requested.Remove(id);
                } else {
                    this.actors[slot] = 0;
                    this.items[slot] = -1;
                    this.free[freeCount++] = slot;
                }
            }
            for (var i = 0; i < labels.Length; i++) {
                var id = labels[i].ActorId;
                if (!this.requested.ContainsKey(id)) continue;
                var slot = this.free[--freeCount];
                this.actors[slot] = id;
                this.items[slot] = i;
            }
        }
        public void Clear() {
            Array.Clear(this.actors, 0, this.actors.Length);
            Array.Fill(this.items, -1);
            this.requested.Clear();
        }
    }
}

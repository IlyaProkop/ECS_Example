using System;
using Game.Domain;
using Game.ECS.Components;
using Scellecs.Morpeh;

namespace Game.ECS.Core {
    // Single-thread: append in producer phase, consume once, clear.
    // A span is borrowed until Clear. No resize, overwrite, or silent loss on overflow.
    internal sealed class FrameBuffer<T> where T : struct {
        private readonly T[] items;
        public int Count { get; private set; }
        public int PeakCount { get; private set; }
        public int Capacity => this.items.Length;
        public ReadOnlySpan<T> Items => this.items.AsSpan(0, this.Count);
        public FrameBuffer(int capacity) {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.items = new T[capacity];
        }
        public void Add(in T item) {
            if (this.Count == this.items.Length) throw new InvalidOperationException($"{typeof(T).Name} frame capacity {this.Capacity} exceeded.");
            this.items[this.Count++] = item;
            this.PeakCount = Math.Max(this.PeakCount, this.Count);
        }
        public void Clear() {
            // Requests retain weak managed entity handles; release them at the phase boundary.
            Array.Clear(this.items, 0, this.Count);
            this.Count = 0;
        }
    }

    internal struct DamageRequest {
        public Entity target;
        public float value;
        public DamageSourceComponent source;
        public DamageKind kind;
    }
    internal struct DamageApplied {
        public float amount;
        public Team sourceTeam;
        public bool targetIsPlayer;
        public bool critical;
    }
}

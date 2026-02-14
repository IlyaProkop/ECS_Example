using System;
using System.Collections.Generic;
using Game.Domain;
using UnityEngine;

namespace Game.ECS.Core {
    // Optional presentation history, independent of ECS entity/filter order. Health, radius,
    // appearance and membership always come from the current authoritative snapshot.
    internal sealed class ActorInterpolationBuffer {
        private readonly ActorView[] current;
        private readonly Vector3[] previousPositions;
        private readonly Dictionary<int, Vector3> lastPositions;
        private int count;
        public ActorInterpolationBuffer(int capacity) {
            this.current = new ActorView[capacity];
            this.previousPositions = new Vector3[capacity];
            this.lastPositions = new Dictionary<int, Vector3>(capacity);
        }
        public void Capture(ReadOnlySpan<ActorView> actors, bool reset) {
            if (actors.Length > this.current.Length) throw new ArgumentException("Actor history capacity exceeded.", nameof(actors));
            for (var i = 0; i < actors.Length; i++) {
                ref readonly var actor = ref actors[i];
                this.previousPositions[i] = !reset && this.lastPositions.TryGetValue(actor.id, out var previous) ? previous : actor.position;
                this.current[i] = actor;
            }
            this.count = actors.Length;
            this.lastPositions.Clear();
            for (var i = 0; i < actors.Length; i++) this.lastPositions.Add(actors[i].id, actors[i].position);
        }
        public int CopyTo(Span<ActorView> destination, float alpha) {
            if (destination.Length < this.count) throw new ArgumentException("Actor output capacity exceeded.", nameof(destination));
            for (var i = 0; i < this.count; i++) {
                var actor = this.current[i];
                var position = alpha >= 1f ? actor.position : alpha <= 0f ? this.previousPositions[i] :
                    Vector3.LerpUnclamped(this.previousPositions[i], actor.position, alpha);
                destination[i] = new ActorView(actor.id, actor.kind, position, actor.radius, actor.health, actor.visualId);
            }
            return this.count;
        }
    }
}

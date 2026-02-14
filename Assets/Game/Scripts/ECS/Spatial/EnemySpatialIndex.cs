using System;
using Scellecs.Morpeh;
using UnityEngine;
using EntityId = Scellecs.Morpeh.EntityId;

namespace Game.Spatial {
    // Combat receives a geometry copy and a weak handle: validate before live ECS access.
    internal readonly struct EnemySpatialSample {
        public readonly Entity Entity;
        public readonly EntityId Identity;
        public readonly int ActorId;
        public readonly Vector2 Position;
        public readonly float Radius;
        public bool IsAlive => !this.Entity.IsNullOrDisposed() && this.Entity.ID == this.Identity;

        internal EnemySpatialSample(Entity entity, EntityId identity, in EnemyGeometrySample geometry) {
            this.Entity = entity;
            this.Identity = identity;
            this.ActorId = geometry.ActorId;
            this.Position = geometry.Position;
            this.Radius = geometry.Radius;
        }
    }

    // Morpeh adapter over value-only spatial storage, rebuilt before movement and combat.
    internal sealed class EnemySpatialIndex {
        private readonly EnemyHandle[] handles;
        private readonly EnemyGeometryIndex geometry;
        public float MaxRadius { get; private set; }
        public float MaxAttackRange { get; private set; }

        public EnemySpatialIndex(int capacity) {
            this.geometry = new EnemyGeometryIndex(capacity);
            this.handles = new EnemyHandle[capacity];
        }

        public void BeginFrame() {
            Array.Clear(this.handles, 0, this.geometry.Count);
            this.geometry.BeginFrame();
            this.MaxRadius = 0f;
            this.MaxAttackRange = 0f;
        }

        // The producer adds each actor once, within the configured finite population budget.
        public void Add(Entity enemy, int actorId, Vector3 position, float radius, float attackRange = 0f) {
            var index = this.geometry.Count;
            if (index == this.geometry.Capacity) throw new InvalidOperationException("Enemy spatial capacity exceeded.");
            var handle = new EnemyHandle(enemy);
            this.geometry.Add(new EnemyGeometrySample(actorId, position, radius));
            this.handles[index] = handle;
            this.MaxRadius = Mathf.Max(this.MaxRadius, radius);
            this.MaxAttackRange = Mathf.Max(this.MaxAttackRange, attackRange);
        }

        // Validate generations once per actor, not once per neighbor pair. The returned view
        // freezes liveness at this boundary: no structural writes/callbacks during motion solving.
        // Stale slots stay in place so the grid/linear order and fallback decision do not change.
        public EnemyGeometryIndex.ReadView BorrowLiveGeometry() {
            for (var i = 0; i < this.geometry.Count; i++) {
                if (!this.handles[i].IsAlive) this.geometry.Disable(i);
            }
            return this.geometry.Borrow();
        }

        public bool TryFindNearest(Vector3 position, out EnemySpatialSample sample) {
            var point = new Vector2(position.x, position.z);
            var distance = float.PositiveInfinity;
            sample = default;
            // One query per weapon cooldown. Equal distances retain insertion order.
            for (var i = 0; i < this.geometry.Count; i++) {
                var candidate = this.geometry.SampleAt(i);
                var squared = (candidate.Position - point).sqrMagnitude;
                if (squared >= distance || !this.handles[i].IsAlive) continue;
                distance = squared;
                sample = this.SampleAt(i, candidate);
            }
            return sample.Entity != null;
        }

        public CircleQuery QueryCircle(Vector3 position, float radius) =>
            new CircleQuery(this, this.geometry.Borrow().QueryCircle(position, radius));

        private EnemySpatialSample SampleAt(int index, in EnemyGeometrySample sample) {
            var handle = this.handles[index];
            return new EnemySpatialSample(handle.Entity, handle.Identity, sample);
        }

        private readonly struct EnemyHandle {
            public readonly Entity Entity;
            public readonly EntityId Identity;
            public bool IsAlive => !this.Entity.IsNullOrDisposed() && this.Entity.ID == this.Identity;
            public EnemyHandle(Entity entity) { this.Entity = entity; this.Identity = entity.ID; }
        }

        public readonly struct CircleQuery {
            private readonly EnemySpatialIndex owner;
            private readonly EnemyGeometryIndex.CircleQuery query;
            internal CircleQuery(EnemySpatialIndex owner, EnemyGeometryIndex.CircleQuery query) {
                this.owner = owner; this.query = query;
            }
            public Enumerator GetEnumerator() => new Enumerator(this.owner, this.query.GetEnumerator());
        }

        public struct Enumerator {
            private readonly EnemySpatialIndex owner;
            private EnemyGeometryIndex.Enumerator cursor;
            internal Enumerator(EnemySpatialIndex owner, EnemyGeometryIndex.Enumerator cursor) {
                this.owner = owner; this.cursor = cursor;
            }
            public EnemySpatialSample Current {
                get {
                    var index = this.cursor.CurrentIndex;
                    return this.owner.SampleAt(index, this.owner.geometry.SampleAt(index));
                }
            }
            public bool MoveNext() => this.cursor.MoveNext();
        }
    }
}

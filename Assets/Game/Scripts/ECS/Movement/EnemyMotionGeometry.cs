using System;
using Game.ECS.Components;
using Game.Spatial;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Movement {
    // Captures live geometry directly into the backend used by this movement step.
    // No Entity handles escape the capture; combat builds its own post-movement index.
    internal sealed class EnemyMotionGeometry : IDisposable {
        private readonly EnemyGeometryIndex managed;
        private readonly NativeEnemyGeometrySnapshot native;
        private Filter actors;
        private Stash<PositionComponent> positions;
        private Stash<HealthComponent> health;
        private Stash<RadiusComponent> radii;
        private Stash<ActorComponent> identities;

        public EnemyMotionGeometry(int capacity, bool supportParallel) {
            this.managed = new EnemyGeometryIndex(capacity);
            if (supportParallel) this.native = new NativeEnemyGeometrySnapshot(capacity);
        }

        public void Initialize(World world) {
            // Preserve the combat index's filter and traversal order, including actors
            // without a path: stationary actors must still separate moving neighbours.
            this.actors = world.Filter.With<EnemyTag>().With<PositionComponent>().With<HealthComponent>()
                .With<RadiusComponent>().With<ActorComponent>().With<EnemyBehaviourComponent>().Without<DestroyTag>().Build();
            this.positions = world.GetStash<PositionComponent>();
            this.health = world.GetStash<HealthComponent>();
            this.radii = world.GetStash<RadiusComponent>();
            this.identities = world.GetStash<ActorComponent>();
        }

        public float Capture(bool parallel) {
            if (parallel) this.native.BeginFrame();
            else this.managed.BeginFrame();
            var maxRadius = 0f;
            foreach (var actor in this.actors) {
                if (this.health.Get(actor).current <= 0f) continue;
                var radius = this.radii.Get(actor).value;
                var sample = new EnemyGeometrySample(this.identities.Get(actor).id, this.positions.Get(actor).value, radius);
                if (parallel) this.native.Add(sample, false);
                else this.managed.Add(sample);
                maxRadius = Mathf.Max(maxRadius, radius);
            }
            return maxRadius;
        }

        public EnemyGeometryIndex.ReadView Managed => this.managed.Borrow();
        public NativeEnemyGeometrySnapshot.ReadView Native => this.native.Borrow();
        public void Dispose() => this.native?.Dispose();
    }
}

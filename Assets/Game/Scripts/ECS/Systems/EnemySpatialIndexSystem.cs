using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;
using UnityEngine;
using Unity.Profiling;

namespace Game.ECS.Systems {
    internal sealed class EnemySpatialIndexSystem : ISystem {
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("Arena.System.EnemySpatialIndex");
        private readonly Game.Spatial.EnemySpatialIndex spatial;

        private Filter enemyFilter;

        private Stash<PositionComponent> positionStash;
        private Stash<HealthComponent> healthStash;
        private Stash<RadiusComponent> radii;
        private Stash<ActorComponent> actors;
        private Stash<EnemyBehaviourComponent> behaviours;

        public EnemySpatialIndexSystem(Game.Spatial.EnemySpatialIndex spatial) {
            this.spatial = spatial;
        }

        public World World { get; set; }

        public void OnAwake() {
            this.enemyFilter = this.World.Filter.With<EnemyTag>().With<PositionComponent>().With<HealthComponent>()
                .With<RadiusComponent>().With<ActorComponent>().With<EnemyBehaviourComponent>().Without<DestroyTag>().Build();

            this.positionStash = this.World.GetStash<PositionComponent>();
            this.healthStash = this.World.GetStash<HealthComponent>();
            this.radii = this.World.GetStash<RadiusComponent>();
            this.actors = this.World.GetStash<ActorComponent>();
            this.behaviours = this.World.GetStash<EnemyBehaviourComponent>();
        }

        public void OnUpdate(float deltaTime) {
            using var sample = UpdateMarker.Auto();
            this.spatial.BeginFrame();

            foreach (var entity in this.enemyFilter) {
                var health = this.healthStash.Get(entity);
                if (health.current <= 0f) {
                    continue;
                }

                var position = this.positionStash.Get(entity).value;
                this.spatial.Add(entity, this.actors.Get(entity).id, position, this.radii.Get(entity).value,
                    this.behaviours.Get(entity).attackRange);
            }
        }

        public void Dispose() {
        }
    }
}

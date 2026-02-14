using Game.ECS.Components;
using Scellecs.Morpeh;

namespace Game.ECS.Systems {
    internal sealed class DestroyMarkedEntitiesSystem : ISystem {
        private Filter entities;
        private readonly Stats.StatStorage stats;
        private readonly Spawning.IEntityRecycler recycler;
        public DestroyMarkedEntitiesSystem(Stats.StatStorage stats = null, Spawning.IEntityRecycler recycler = null) {
            this.stats = stats; this.recycler = recycler;
        }
        public World World { get; set; }
        public void OnAwake() => this.entities = this.World.Filter.With<DestroyTag>().Build();
        public void OnUpdate(float deltaTime) {
            foreach (var entity in this.entities) {
                if (this.recycler != null && this.recycler.TryRecycle(entity)) continue;
                this.stats?.Release(entity); this.World.RemoveEntity(entity);
            }
        }
        public void Dispose() { }
    }
}

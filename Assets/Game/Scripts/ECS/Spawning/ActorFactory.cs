using Game.Domain;
using Game.ECS.Components;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Spawning {
    // One identity sequence for all actor kinds in one world. IDs are never recycled.
    internal sealed class ActorFactory {
        private readonly World world;
        private readonly Stash<ActorComponent> actors;
        private readonly Stash<PositionComponent> positions;
        private readonly Stash<RadiusComponent> radii;
        private readonly Stash<HealthComponent> health;
        private readonly Stats.StatStorage stats;
        private int nextId;

        public ActorFactory(World world, Stats.StatStorage stats) {
            this.world = world;
            this.stats = stats;
            stats.Attach(world);
            this.actors = world.GetStash<ActorComponent>();
            this.positions = world.GetStash<PositionComponent>();
            this.radii = world.GetStash<RadiusComponent>();
            this.health = world.GetStash<HealthComponent>();
        }

        public Entity Create(ActorKind kind, Vector3 position, float radius, int visualId = 0, Entity prepared = null) {
            var id = checked(this.nextId + 1);
            var entity = prepared ?? this.world.CreateEntity();
            this.nextId = id;
            this.actors.Set(entity, new ActorComponent { id = id, kind = kind,
                visualId = visualId == 0 ? ActorVisualIds.DefaultFor(kind) : visualId });
            this.positions.Set(entity, new PositionComponent { value = position });
            this.radii.Set(entity, new RadiusComponent { value = radius });
            return entity;
        }

        public void SetVitals(Entity entity, float maxHealth, float armor, float speed) {
            this.health.Set(entity, new HealthComponent { current = maxHealth, max = maxHealth });
            this.stats.Register(entity, speed, armor);
        }
    }
}

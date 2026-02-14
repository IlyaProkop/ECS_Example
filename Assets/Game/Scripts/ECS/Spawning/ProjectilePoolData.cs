using Game.ECS.Components;
using Scellecs.Morpeh;

namespace Game.ECS.Spawning {
    // Keep hot value storage, but remove all active membership and stat ownership on return.
    internal sealed class ProjectilePoolData {
        private readonly Stash<ActorComponent> actors;
        private readonly Stash<ProjectileTag> projectiles;
        private readonly Stash<DestroyTag> destroyed;
        private readonly Stash<StatOwnerComponent> stats;
        private readonly Stash<HealthComponent> health;
        private readonly Stash<PositionComponent> positions;
        private readonly Stash<RadiusComponent> radii;
        private readonly Stash<ProjectileComponent> motion;
        private readonly Stash<ProjectilePayloadComponent> payload;
        private readonly Stash<DamageSourceComponent> source;
        private readonly Stash<MoveSpeedComponent> speed;
        private readonly Stash<DefenseComponent> defense;
        private readonly Stash<DamageMultiplierComponent> multiplier;
        public ProjectilePoolData(World world) {
            this.actors = world.GetStash<ActorComponent>(); this.projectiles = world.GetStash<ProjectileTag>();
            this.destroyed = world.GetStash<DestroyTag>(); this.stats = world.GetStash<StatOwnerComponent>();
            this.health = world.GetStash<HealthComponent>(); this.positions = world.GetStash<PositionComponent>();
            this.radii = world.GetStash<RadiusComponent>(); this.motion = world.GetStash<ProjectileComponent>();
            this.payload = world.GetStash<ProjectilePayloadComponent>(); this.source = world.GetStash<DamageSourceComponent>();
            this.speed = world.GetStash<MoveSpeedComponent>(); this.defense = world.GetStash<DefenseComponent>();
            this.multiplier = world.GetStash<DamageMultiplierComponent>();
        }
        public void Prepare(Entity entity) {
            this.Reset(entity);
            this.actors.Add(entity); this.projectiles.Add(entity); this.stats.Add(entity); this.destroyed.Add(entity);
        }
        public void Reset(Entity entity) {
            this.actors.Remove(entity); this.projectiles.Remove(entity); this.destroyed.Remove(entity);
            this.stats.Remove(entity); this.health.Remove(entity);
            this.positions.Set(entity, default); this.radii.Set(entity, default); this.motion.Set(entity, default);
            this.payload.Set(entity, default); this.source.Set(entity, default); this.speed.Set(entity, default);
            this.defense.Set(entity, default); this.multiplier.Set(entity, default);
        }
    }
}

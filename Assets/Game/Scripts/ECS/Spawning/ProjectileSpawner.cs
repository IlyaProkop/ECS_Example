using Game.Domain;
using Game.ECS.Components;
using Scellecs.Morpeh;

namespace Game.ECS.Spawning {
    internal sealed class ProjectileSpawner : IProjectileSpawner {
        private readonly ActorFactory actors;
        private readonly Stash<ProjectileTag> projectiles;
        private readonly Stash<ProjectileComponent> motion;
        private readonly Stash<DamageSourceComponent> sources;
        private readonly Stash<ProjectilePayloadComponent> payloads;
        private readonly Stats.StatStorage stats;
        private readonly Projectiles.ProjectileRuntimeCatalog catalog;
        private readonly ProjectilePool pool;
        private readonly World world;
        private readonly Stash<ActorComponent> identities;
        public ProjectileSpawner(World world, ActorFactory actors, Stats.StatStorage stats, Projectiles.ProjectileRuntimeCatalog catalog, ProjectilePool pool = null) {
            this.pool = pool;
            this.world = world; this.identities = world.GetStash<ActorComponent>();
            this.actors = actors;
            this.stats = stats; this.catalog = catalog;
            this.projectiles = world.GetStash<ProjectileTag>();
            this.motion = world.GetStash<ProjectileComponent>();
            this.sources = world.GetStash<DamageSourceComponent>();
            this.payloads = world.GetStash<ProjectilePayloadComponent>();
        }
        public Entity Spawn(in ProjectileSpawn request) {
            var definition = this.catalog.Get(request.DefinitionIndex);
            var targetActorId = request.TargetActorId;
            if (targetActorId == 0 && request.Target != default && this.world.TryGetEntity(request.Target, out var target) && this.identities.Has(target))
                targetActorId = this.identities.Get(target).id;
            this.stats.EnsureCapacity();
            var entity = this.pool?.Rent();
            try {
                entity = this.actors.Create(ActorKind.Projectile, request.Position, request.Radius, definition.Definition.VisualId, entity);
                this.projectiles.Add(entity);
                this.sources.Set(entity, request.Source);
                this.payloads.Set(entity, new ProjectilePayloadComponent { definitionIndex = request.DefinitionIndex,
                    target = request.Target, targetActorId = targetActorId });
                this.stats.Register(entity, request.Speed, 0f);
                foreach (var status in definition.SpawnStatuses)
                    this.stats.TryApply(entity, new Stats.EffectSource(request.Source.owner, status.Definition.Id), status, request.Source, out _);
                this.stats.RecalculateDirty();
                this.motion.Set(entity, new ProjectileComponent {
                    previousPosition = request.Position, direction = request.Direction,
                    speed = request.Speed, damage = request.Damage, radius = request.Radius,
                    remainingLifetime = request.Lifetime
                });
                return entity;
            } catch {
                if (entity != null) {
                    if (this.pool != null) this.pool.TryRecycle(entity);
                    else { this.stats.Release(entity); entity.Dispose(); }
                }
                throw;
            }
        }
    }
}

using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Projectiles {
    internal sealed class ProjectileMotionSystem : ISystem {
        private readonly EntityLookup entities;
        private readonly ProjectileRuntimeCatalog catalog;
        private Filter projectiles;
        private Stash<PositionComponent> positions;
        private Stash<ProjectileComponent> motions;
        private Stash<ProjectilePayloadComponent> payloads;
        private Stash<MoveSpeedComponent> speeds;
        private Stash<HealthComponent> health;
        private Stash<ActorComponent> identities;
        private Stash<DestroyTag> destroyed;
        private Stash<GameStateComponent> states;
        public ProjectileMotionSystem(EntityLookup entities, ProjectileRuntimeCatalog catalog) { this.entities = entities; this.catalog = catalog; }
        public World World { get; set; }
        public void OnAwake() {
            this.projectiles = this.World.Filter.With<ProjectileTag>().With<ProjectileComponent>().With<PositionComponent>()
                .With<ProjectilePayloadComponent>().Without<DestroyTag>().Build();
            this.positions = this.World.GetStash<PositionComponent>(); this.motions = this.World.GetStash<ProjectileComponent>();
            this.payloads = this.World.GetStash<ProjectilePayloadComponent>(); this.speeds = this.World.GetStash<MoveSpeedComponent>();
            this.health = this.World.GetStash<HealthComponent>(); this.destroyed = this.World.GetStash<DestroyTag>();
            this.identities = this.World.GetStash<ActorComponent>();
            this.states = this.World.GetStash<GameStateComponent>();
        }
        public void OnUpdate(float deltaTime) {
            if (!GameStateHelper.IsCombat(this.entities, this.states)) return;
            foreach (var entity in this.projectiles) {
                var motion = this.motions.Get(entity);
                var position = this.positions.Get(entity);
                var payload = this.payloads.Get(entity);
                var trajectory = this.catalog.Get(payload.definitionIndex).Definition.Motion;
                var targetPosition = default(Vector3);
                var targetAlive = trajectory.TracksTarget && this.World.TryGetEntity(payload.target, out var target) &&
                    !target.IsNullOrDisposed() && this.positions.Has(target) && this.health.Has(target) &&
                    payload.targetActorId > 0 && this.identities.Has(target) && this.identities.Get(target).id == payload.targetActorId &&
                    this.health.Get(target).current > 0f && !this.destroyed.Has(target);
                if (targetAlive && this.World.TryGetEntity(payload.target, out var live)) targetPosition = this.positions.Get(live).value;
                motion.direction = trajectory.Direction(position.value, motion.direction, targetPosition, targetAlive);
                motion.previousPosition = position.value;
                motion.speed = this.speeds.Get(entity).value;
                position.value += motion.direction * (motion.speed * Mathf.Min(deltaTime, Mathf.Max(0f, motion.remainingLifetime)));
                motion.remainingLifetime -= deltaTime;
                this.positions.Set(entity, position); this.motions.Set(entity, motion);
            }
        }
        public void Dispose() { }
    }
}

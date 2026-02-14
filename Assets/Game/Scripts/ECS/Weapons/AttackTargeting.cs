using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.Spatial;
using Scellecs.Morpeh;
using UnityEngine;
using EntityId = Scellecs.Morpeh.EntityId;

namespace Game.ECS.Weapons {
    internal readonly struct AimSolution {
        public readonly Vector3 Direction;
        public readonly EntityId Target;
        public readonly int TargetActorId;
        public AimSolution(Vector3 direction, EntityId target = default, int targetActorId = 0) {
            this.Direction = direction; this.Target = target; this.TargetActorId = targetActorId;
        }
    }
    internal interface IAttackTargeting {
        bool TryAim(Entity shooter, in AttackAim intent, float range, out AimSolution aim);
    }
    internal sealed class TargetQueries {
        private readonly World world;
        private readonly EntityLookup entities;
        private readonly EnemySpatialIndex enemies;
        private readonly Filter actors;
        private readonly Stash<ActorComponent> identities;
        private readonly Stash<PositionComponent> positions;
        private readonly Stash<HealthComponent> health;
        private readonly Stash<DestroyTag> destroyed;
        private readonly Stash<DamageSourceComponent> sources;
        public TargetQueries(World world, EntityLookup entities, EnemySpatialIndex enemies) {
            this.world = world; this.entities = entities; this.enemies = enemies;
            this.actors = world.Filter.With<ActorComponent>().With<HealthComponent>().Without<DestroyTag>().Build();
            this.identities = world.GetStash<ActorComponent>(); this.positions = world.GetStash<PositionComponent>();
            this.health = world.GetStash<HealthComponent>(); this.destroyed = world.GetStash<DestroyTag>();
            this.sources = world.GetStash<DamageSourceComponent>();
        }
        public Entity Nearest(Entity shooter) {
            if (this.sources.Get(shooter).team == Team.Enemy) return this.entities.player;
            if (this.sources.Get(shooter).team == Team.Player && this.enemies.TryFindNearest(this.positions.Get(shooter).value, out var target)) return target.Entity;
            return null;
        }
        public Entity Find(int actorId) {
            foreach (var actor in this.actors) if (this.identities.Get(actor).id == actorId) return actor;
            return null;
        }
        public bool TryAimAt(Entity shooter, Entity target, float range, out AimSolution aim) {
            aim = default;
            if (target.IsNullOrDisposed() || !this.world.TryGetEntity(target.ID, out var own) || !ReferenceEquals(own, target) ||
                this.destroyed.Has(target) || !this.health.Has(target) || this.health.Get(target).current <= 0f ||
                !this.positions.Has(target) || !this.sources.Has(target) || !this.identities.Has(target)) return false;
            var team = this.sources.Get(shooter).team; var other = this.sources.Get(target).team;
            if (team == Team.Neutral || other == Team.Neutral || team == other) return false;
            var delta = this.positions.Get(target).value - this.positions.Get(shooter).value; delta.y = 0f;
            if (delta.sqrMagnitude > (double)range * range) return false;
            var direction = delta.normalized;
            if (direction.sqrMagnitude < .001f) return false;
            aim = new AimSolution(direction, target.ID, this.identities.Get(target).id); return true;
        }
    }
    internal sealed class NearestHostileTargeting : IAttackTargeting {
        private readonly TargetQueries queries;
        public NearestHostileTargeting(TargetQueries queries) => this.queries = queries;
        public bool TryAim(Entity shooter, in AttackAim intent, float range, out AimSolution aim) =>
            this.queries.TryAimAt(shooter, this.queries.Nearest(shooter), range, out aim);
    }
    internal sealed class ActorTargeting : IAttackTargeting {
        private readonly TargetQueries queries;
        public ActorTargeting(TargetQueries queries) => this.queries = queries;
        public bool TryAim(Entity shooter, in AttackAim intent, float range, out AimSolution aim) =>
            this.queries.TryAimAt(shooter, this.queries.Find(intent.actorId), range, out aim);
    }
    internal sealed class DirectionTargeting : IAttackTargeting {
        public bool TryAim(Entity shooter, in AttackAim intent, float range, out AimSolution aim) {
            aim = default;
            var direction = new Vector3(intent.direction.x, 0f, intent.direction.y).normalized;
            if (direction.sqrMagnitude < .001f || float.IsNaN(direction.x) || float.IsNaN(direction.z)) return false;
            aim = new AimSolution(direction); return true;
        }
    }
    // Small prebuilt table. Adding a policy does not change cooldown/spawning/statistics.
    internal sealed class AttackTargeting {
        private readonly IAttackTargeting[] policies;
        public AttackTargeting(World world, EntityLookup entities, EnemySpatialIndex enemies) {
            var queries = new TargetQueries(world, entities, enemies);
            this.policies = new IAttackTargeting[] { new NearestHostileTargeting(queries), new ActorTargeting(queries), new DirectionTargeting() };
        }
        public bool TryAim(Entity shooter, in AttackAim intent, float range, out AimSolution aim) {
            aim = default;
            return (uint)intent.kind < this.policies.Length && this.policies[(int)intent.kind].TryAim(shooter, intent, range, out aim);
        }
    }
}

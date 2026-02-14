using System;
using System.Collections.ObjectModel;
using Game.Domain.Stats;
using UnityEngine;

namespace Game.Domain {
    // Immutable strategies see values only. Per-shot state belongs to ECS components.
    public abstract class ProjectileMotionDefinition {
        public virtual bool TracksTarget => false;
        public abstract Vector3 Direction(Vector3 position, Vector3 direction, Vector3 target, bool targetAlive);
    }
    public sealed class StraightProjectileMotion : ProjectileMotionDefinition {
        public override Vector3 Direction(Vector3 position, Vector3 direction, Vector3 target, bool targetAlive) => direction;
    }
    public sealed class HomingProjectileMotion : ProjectileMotionDefinition {
        public override bool TracksTarget => true;
        public override Vector3 Direction(Vector3 position, Vector3 direction, Vector3 target, bool targetAlive) {
            var delta = target - position; delta.y = 0f;
            return targetAlive && delta.sqrMagnitude > .000001f ? delta.normalized : direction;
        }
    }
    public abstract class ProjectileHitDefinition {
        public abstract int DamageRequestCapacity { get; }
        public virtual bool UsesPeriodicDamage => false;
    }
    public sealed class DirectProjectileDamage : ProjectileHitDefinition {
        public DamageKind Kind { get; }
        public DirectProjectileDamage(DamageKind kind = DamageKind.Projectile) { this.Kind = kind; }
        public override int DamageRequestCapacity => 1;
    }
    public sealed class ProjectileStatusHit : ProjectileHitDefinition {
        public StatusEffectDefinition Status { get; }
        public ProjectileStatusHit(StatusEffectDefinition status) => this.Status = status ?? throw new ArgumentNullException(nameof(status));
        public override int DamageRequestCapacity => 0;
        public override bool UsesPeriodicDamage => this.Status.PeriodicDamage != null;
    }
    public sealed class ProjectileDefinition {
        public float Speed { get; }
        public float Damage { get; }
        public float Radius { get; }
        public float Lifetime { get; }
        public int VisualId { get; }
        public ProjectileMotionDefinition Motion { get; }
        public ReadOnlyCollection<ProjectileHitDefinition> Hits { get; }
        public ReadOnlyCollection<StatusEffectDefinition> SpawnStatuses { get; }
        public int HitDamageCapacity { get; }
        public ProjectileDefinition(float speed, float damage, float radius, float lifetime,
            ProjectileMotionDefinition motion, ProjectileHitDefinition[] hits, StatusEffectDefinition[] spawnStatuses = null,
            int visualId = ActorVisualIds.Projectile) {
            StatValidation.Finite(speed); StatValidation.Finite(damage); StatValidation.Finite(radius); StatValidation.Finite(lifetime);
            if (speed < 0f || damage < 0f || radius <= 0f || lifetime <= 0f || visualId <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
            if (hits == null || hits.Length < 1 || hits.Length > 16) throw new ArgumentException("Configure 1 to 16 hit effects.");
            var copy = (ProjectileHitDefinition[])hits.Clone();
            foreach (var hit in copy) {
                if (hit == null || hit.DamageRequestCapacity < 0) throw new ArgumentException("Invalid hit effect.");
                this.HitDamageCapacity = checked(this.HitDamageCapacity + hit.DamageRequestCapacity);
            }
            var statuses = (StatusEffectDefinition[])(spawnStatuses ?? Array.Empty<StatusEffectDefinition>()).Clone();
            if (statuses.Length > 16) throw new ArgumentException("At most 16 spawn statuses.");
            foreach (var status in statuses) if (status == null) throw new ArgumentException("Missing spawn status.");
            this.Speed = speed; this.Damage = damage; this.Radius = radius; this.Lifetime = lifetime;
            this.Motion = motion ?? throw new ArgumentNullException(nameof(motion)); this.VisualId = visualId;
            this.Hits = Array.AsReadOnly(copy); this.SpawnStatuses = Array.AsReadOnly(statuses);
        }
    }
    public sealed class WeaponDefinition {
        public int Id { get; }
        public float Interval { get; }
        public float Range { get; }
        public ProjectileDefinition Projectile { get; }
        public WeaponDefinition(float interval, ProjectileDefinition projectile, int id = 1, float range = 1000f) {
            StatValidation.Finite(interval); StatValidation.Finite(range);
            if (interval <= 0f || range <= 0f || id <= 0) throw new ArgumentOutOfRangeException(nameof(interval));
            this.Id = id; this.Range = range;
            this.Interval = interval; this.Projectile = projectile ?? throw new ArgumentNullException(nameof(projectile));
        }
    }
}

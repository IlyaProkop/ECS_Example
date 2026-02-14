using System;
using Game.Domain;
using Game.Domain.Stats;
using UnityEngine;

namespace Game.Config {
    public enum ProjectileMotionMode { Straight, Homing }
    [CreateAssetMenu(menuName = "Game/Combat/Projectile", fileName = "Projectile")]
    public class ProjectileAsset : ScriptableObject {
        [Min(0f)] public float speed = 16f, damage = 25f;
        [Min(.001f)] public float radius = .2f, lifetime = 4f;
        public int visualId = ActorVisualIds.Projectile;
        public ProjectileMotionMode motion;
        public ProjectileHitAsset[] hitEffects;
        public StatusEffectAsset[] spawnStatuses = Array.Empty<StatusEffectAsset>();
        // The built-in selector is authoring only. New motion assets override this factory.
        protected virtual ProjectileMotionDefinition CreateMotion() => this.motion switch {
            ProjectileMotionMode.Straight => new StraightProjectileMotion(),
            ProjectileMotionMode.Homing => new HomingProjectileMotion(),
            _ => throw new InvalidOperationException("Unknown projectile motion.")
        };
        public ProjectileDefinition CreateDefinition() {
            if (this.hitEffects == null || this.hitEffects.Length == 0) throw new InvalidOperationException("Projectile requires hit effects.");
            var hits = new ProjectileHitDefinition[this.hitEffects.Length];
            for (var i = 0; i < hits.Length; i++) hits[i] = this.hitEffects[i] != null ? this.hitEffects[i].CreateDefinition()
                : throw new InvalidOperationException("Missing projectile hit effect.");
            var statuses = new StatusEffectDefinition[this.spawnStatuses?.Length ?? 0];
            for (var i = 0; i < statuses.Length; i++) statuses[i] = this.spawnStatuses[i] != null ? this.spawnStatuses[i].CreateDefinition()
                : throw new InvalidOperationException("Missing projectile spawn status.");
            return new ProjectileDefinition(this.speed, this.damage, this.radius, this.lifetime, this.CreateMotion(), hits, statuses, this.visualId);
        }
    }
}

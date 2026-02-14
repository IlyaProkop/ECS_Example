using System;

namespace Game.Domain {
    // Immutable content. Distances are measured between actor centers in the arena plane.
    public sealed class EnemyDefinition {
        public int Id { get; }
        public int VisualId { get; }
        public float MaxHealth { get; }
        public float MoveSpeed { get; }
        public float Radius { get; }
        public float StopDistance { get; }
        public float AttackRange { get; }
        public float DamagePerSecond { get; }
        public float Armor { get; }
        public WeaponDefinition Weapon { get; }
        public Abilities.AbilityLoadout Abilities { get; }

        public EnemyDefinition(int id, float maxHealth, float moveSpeed, float radius,
            float stopDistance, float attackRange, float damagePerSecond, float armor, int visualId = ActorVisualIds.Enemy,
            WeaponDefinition weapon = null, Abilities.AbilityLoadout abilities = null) {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (visualId <= 0) throw new ArgumentOutOfRangeException(nameof(visualId));
            Check(maxHealth, nameof(maxHealth), true); Check(radius, nameof(radius), true);
            Check(moveSpeed, nameof(moveSpeed)); Check(stopDistance, nameof(stopDistance));
            Check(attackRange, nameof(attackRange)); Check(damagePerSecond, nameof(damagePerSecond));
            Check(armor, nameof(armor));
            if (attackRange < stopDistance) throw new ArgumentException("Attack range must reach the stopping distance.");
            this.Id = id; this.MaxHealth = maxHealth; this.MoveSpeed = moveSpeed; this.Radius = radius;
            this.VisualId = visualId;
            this.StopDistance = stopDistance; this.AttackRange = attackRange;
            this.DamagePerSecond = damagePerSecond; this.Armor = armor;
            this.Weapon = weapon;
            this.Abilities = abilities ?? new Abilities.AbilityLoadout();
        }
        private static void Check(float value, string name, bool positive = false) {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || (positive && value == 0f))
                throw new ArgumentOutOfRangeException(name);
        }
    }
}

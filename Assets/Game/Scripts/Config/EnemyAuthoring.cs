using System;
using Game.Domain;
using UnityEngine;

namespace Game.Config {
    [Serializable]
    public sealed class EnemyAuthoring {
        [Min(1)] public int id = 1;
        [Min(1)] public int visualId = ActorVisualIds.Enemy;
        [Min(1f)] public float maxHealth = 60f;
        [Min(0f)] public float moveSpeed = 2.8f;
        [Min(0.1f)] public float radius = 0.5f;
        [Min(0f)] public float stopDistance = 1.5f;
        [Min(0f)] public float attackRange = 1.55f;
        [Min(0f)] public float damagePerSecond = 5f;
        [Min(0f)] public float armor;
        public WeaponAsset weapon;
        public AbilityAsset[] abilities = Array.Empty<AbilityAsset>();
        public EnemyDefinition CreateDefinition() => this.CreateDefinition(this.weapon != null ? this.weapon.CreateDefinition() : null, new AbilityAuthoringCache().Loadout(this.abilities));
        internal EnemyDefinition CreateDefinition(WeaponDefinition weapon, Game.Domain.Abilities.AbilityLoadout abilities) => new EnemyDefinition(this.id, this.maxHealth, this.moveSpeed,
            this.radius, this.stopDistance, this.attackRange, this.damagePerSecond, this.armor, this.visualId, weapon, abilities);
    }
}

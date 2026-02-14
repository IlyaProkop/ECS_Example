using System;
using Game.Domain.Abilities;
using UnityEngine;

namespace Game.Domain {
    // Transfer object used only at composition time. A running session never retains it.
    public struct SimulationSettingsData {
        public EnemyDefinition[] enemyTypes;
        public Stats.StatCatalog statCatalog;
        public AbilityDefinition playerAbility;
        public AbilityLoadout playerAbilities;
        public WeaponDefinition playerWeapon;
        public WeaponDefinition[] availableWeapons;
        public float playerMaxHealth;
        public float playerMoveSpeed;
        public float playerRadius;
        public float projectileInterval;
        public float projectileSpeed;
        public float projectileRadius;
        public float projectileDamage;
        public float projectileLifetime;
        public float enemyPathCellSize;
        public float enemyPathRepathInterval;
        public float enemyPathRepathJitter;
        public int enemyPathMaxRepathsPerFrame;
        public float enemyPathWaypointReachDistance;
        public float enemyPathTargetMoveThreshold;
        public float enemyPathSearchPadding;
        public float enemyPathMaxSearchSize;
        public int enemyPathMaxNodes;
        public float enemySpawnInterval;
        public float coinDropChance;
        public float coinPickupRadius;
        public float coinRadius;
        public Vector2 arenaHalfSize;
        public int enemyCount;
        public int victoryReward;
        public float resultsDelay;
        public int randomSeed;
        public float playerArmor;
        public float criticalChance;
        public float criticalMultiplier;
        public float obstacleLookupCellSize;
        public Vector3[] obstaclePositions;
        public Vector3 obstacleScale;
        public int enemyPathMaxWaypoints;
    }

    // Immutable, validated session definition. Unity value types only; no UnityEngine.Object.
    public sealed class SimulationSettings {
        public System.Collections.ObjectModel.ReadOnlyCollection<EnemyDefinition> EnemyTypes { get; }
        public float MaxEnemyRadius { get; }
        public EnemyDefinition EnemyForSpawn(int index) {
            if ((uint)index >= this.enemyCount) throw new ArgumentOutOfRangeException(nameof(index));
            return this.EnemyTypes[index % this.EnemyTypes.Count];
        }
        public Stats.StatCatalog StatCatalog { get; }
        public AbilityDefinition PlayerAbility => this.PlayerAbilities.Slots[0];
        public AbilityLoadout PlayerAbilities { get; }
        public System.Collections.ObjectModel.ReadOnlyCollection<AbilityDefinition> AbilityDefinitions { get; }
        public int AbilityCastCapacity { get; }
        public WeaponDefinition PlayerWeapon { get; }
        public WeaponSet Weapons { get; }
        public int ArmedActorCapacity { get; }
        public float MaxProjectileRadius { get; }
        public int StatOwnerCapacity => 1 + this.enemyCount + this.ProjectileCapacity;
        public readonly float playerMaxHealth;
        public readonly float playerMoveSpeed;
        public readonly float playerRadius;
        public readonly float projectileInterval;
        public readonly float projectileSpeed;
        public readonly float projectileRadius;
        public readonly float projectileDamage;
        public readonly float projectileLifetime;
        public readonly float enemyPathCellSize;
        public readonly float enemyPathRepathInterval;
        public readonly float enemyPathRepathJitter;
        public readonly int enemyPathMaxRepathsPerFrame;
        public readonly float enemyPathWaypointReachDistance;
        public readonly float enemyPathTargetMoveThreshold;
        public readonly float enemyPathSearchPadding;
        public readonly float enemyPathMaxSearchSize;
        public readonly int enemyPathMaxNodes;
        public readonly float enemySpawnInterval;
        public readonly float coinDropChance;
        public readonly float coinPickupRadius;
        public readonly float coinRadius;
        public readonly Vector2 arenaHalfSize;
        public readonly int enemyCount;
        public readonly int victoryReward;
        public readonly float resultsDelay;
        public readonly int randomSeed;
        public readonly float playerArmor;
        public readonly float criticalChance;
        public readonly float criticalMultiplier;
        public readonly float obstacleLookupCellSize;
        public readonly System.Collections.ObjectModel.ReadOnlyCollection<Vector3> obstaclePositions;
        public readonly Vector3 obstacleScale;
        public readonly int enemyPathMaxWaypoints;
        public Vector3 PlayerSpawn => new Vector3(0f, 0f, -this.arenaHalfSize.y - 3f);
        public int ProjectileCapacity { get; }
        public int DamageCapacity { get; }
        public NavigationSettings Navigation { get; }
        public ArenaSettings Arena { get; }
        public int ViewCapacity => 1 + this.enemyCount * 2 + this.ProjectileCapacity;

        public SimulationSettings(in SimulationSettingsData data) {
            this.PlayerWeapon = data.playerWeapon ?? new WeaponDefinition(data.projectileInterval,
                new ProjectileDefinition(data.projectileSpeed, data.projectileDamage, data.projectileRadius, data.projectileLifetime,
                    new StraightProjectileMotion(), new ProjectileHitDefinition[] { new DirectProjectileDamage() }));
            this.StatCatalog = data.statCatalog ?? Stats.StatCatalog.Default;
            var speedDefinition = this.StatCatalog.Definitions[this.StatCatalog.IndexOf(Stats.StatIds.MoveSpeed)];
            var armorDefinition = this.StatCatalog.Definitions[this.StatCatalog.IndexOf(Stats.StatIds.Armor)];
            if (speedDefinition.Min < 0f || armorDefinition.Min < 0f) throw new ArgumentException("Movement speed and armor must be nonnegative.");
            NonNegative(data.playerMaxHealth, nameof(data.playerMaxHealth));
            NonNegative(data.playerMoveSpeed, nameof(data.playerMoveSpeed));
            NonNegative(data.playerRadius, nameof(data.playerRadius));
            NonNegative(data.projectileInterval, nameof(data.projectileInterval));
            NonNegative(data.projectileSpeed, nameof(data.projectileSpeed));
            NonNegative(data.projectileRadius, nameof(data.projectileRadius));
            NonNegative(data.projectileDamage, nameof(data.projectileDamage));
            NonNegative(data.projectileLifetime, nameof(data.projectileLifetime));
            NonNegative(data.enemyPathCellSize, nameof(data.enemyPathCellSize));
            NonNegative(data.enemyPathRepathInterval, nameof(data.enemyPathRepathInterval));
            NonNegative(data.enemyPathRepathJitter, nameof(data.enemyPathRepathJitter));
            NonNegative(data.enemyPathWaypointReachDistance, nameof(data.enemyPathWaypointReachDistance));
            NonNegative(data.enemyPathTargetMoveThreshold, nameof(data.enemyPathTargetMoveThreshold));
            NonNegative(data.enemyPathSearchPadding, nameof(data.enemyPathSearchPadding));
            NonNegative(data.enemyPathMaxSearchSize, nameof(data.enemyPathMaxSearchSize));
            NonNegative(data.enemySpawnInterval, nameof(data.enemySpawnInterval));
            NonNegative(data.coinDropChance, nameof(data.coinDropChance));
            NonNegative(data.coinPickupRadius, nameof(data.coinPickupRadius));
            NonNegative(data.coinRadius, nameof(data.coinRadius));
            NonNegative(data.resultsDelay, nameof(data.resultsDelay));
            NonNegative(data.playerArmor, nameof(data.playerArmor));
            NonNegative(data.criticalChance, nameof(data.criticalChance));
            NonNegative(data.criticalMultiplier, nameof(data.criticalMultiplier));
            NonNegative(data.obstacleLookupCellSize, nameof(data.obstacleLookupCellSize));
            if (data.enemyCount < 1 || data.enemyCount > 10000) throw new ArgumentOutOfRangeException(nameof(data.enemyCount));
            if (data.enemyPathMaxWaypoints < 4 || data.enemyPathMaxWaypoints > 1024) throw new ArgumentOutOfRangeException(nameof(data.enemyPathMaxWaypoints));
            if (data.enemyPathMaxNodes < 128 || data.enemyPathMaxNodes > 65536) throw new ArgumentOutOfRangeException(nameof(data.enemyPathMaxNodes));
            if (data.enemyPathMaxRepathsPerFrame < 1) throw new ArgumentOutOfRangeException(nameof(data.enemyPathMaxRepathsPerFrame));
            if (data.victoryReward < 0) throw new ArgumentOutOfRangeException(nameof(data.victoryReward));
            Positive(data.playerMaxHealth, nameof(data.playerMaxHealth));
            Positive(data.playerRadius, nameof(data.playerRadius));
            Positive(data.projectileInterval, nameof(data.projectileInterval));
            Positive(data.enemySpawnInterval, nameof(data.enemySpawnInterval));
            Positive(data.enemyPathCellSize, nameof(data.enemyPathCellSize));
            Positive(data.obstacleLookupCellSize, nameof(data.obstacleLookupCellSize));
            Positive(data.arenaHalfSize.x, nameof(data.arenaHalfSize));
            Positive(data.arenaHalfSize.y, nameof(data.arenaHalfSize));
            Positive(data.obstacleScale.x, nameof(data.obstacleScale));
            Positive(data.obstacleScale.z, nameof(data.obstacleScale));
            if (data.criticalChance > 1f || data.coinDropChance > 1f || data.enemyPathRepathJitter > 1f || data.criticalMultiplier < 1f)
                throw new ArgumentException("Probabilities must be in [0, 1] and critical multiplier >= 1.");
            if (data.obstaclePositions != null) foreach (var position in data.obstaclePositions) {
                Finite(position.x, nameof(data.obstaclePositions));
                Finite(position.y, nameof(data.obstaclePositions));
                Finite(position.z, nameof(data.obstaclePositions));
            }
            if (data.enemyTypes == null || data.enemyTypes.Length == 0 || data.enemyTypes.Length > 32)
                throw new ArgumentException("Configure between 1 and 32 enemy types.", nameof(data.enemyTypes));
            var enemies = (EnemyDefinition[])data.enemyTypes.Clone();
            var ids = new System.Collections.Generic.HashSet<int>();
            foreach (var enemy in enemies) {
                if (enemy == null || !ids.Add(enemy.Id)) throw new ArgumentException("Enemy types must have unique positive IDs.");
                if (enemy.Radius + 0.5f >= Math.Min(data.arenaHalfSize.x, data.arenaHalfSize.y))
                    throw new ArgumentException("Enemy does not fit inside the spawn area.");
                if (enemy.AttackRange < enemy.Radius + data.playerRadius)
                    throw new ArgumentException("Enemy attack range must reach a non-overlapping player.");
                this.MaxEnemyRadius = Math.Max(this.MaxEnemyRadius, enemy.Radius);
            }
            this.EnemyTypes = Array.AsReadOnly(enemies);
            this.playerMaxHealth = data.playerMaxHealth;
            this.playerMoveSpeed = data.playerMoveSpeed;
            this.playerRadius = data.playerRadius;
            this.projectileInterval = this.PlayerWeapon.Interval;
            this.projectileSpeed = this.PlayerWeapon.Projectile.Speed;
            this.projectileRadius = this.PlayerWeapon.Projectile.Radius;
            this.projectileDamage = this.PlayerWeapon.Projectile.Damage;
            this.projectileLifetime = this.PlayerWeapon.Projectile.Lifetime;
            this.enemyPathCellSize = data.enemyPathCellSize;
            this.enemyPathRepathInterval = data.enemyPathRepathInterval;
            this.enemyPathRepathJitter = data.enemyPathRepathJitter;
            this.enemyPathMaxRepathsPerFrame = data.enemyPathMaxRepathsPerFrame;
            this.enemyPathWaypointReachDistance = data.enemyPathWaypointReachDistance;
            this.enemyPathTargetMoveThreshold = data.enemyPathTargetMoveThreshold;
            this.enemyPathSearchPadding = data.enemyPathSearchPadding;
            this.enemyPathMaxSearchSize = data.enemyPathMaxSearchSize;
            this.enemyPathMaxNodes = data.enemyPathMaxNodes;
            this.enemySpawnInterval = data.enemySpawnInterval;
            this.coinDropChance = data.coinDropChance;
            this.coinPickupRadius = data.coinPickupRadius;
            this.coinRadius = data.coinRadius;
            this.arenaHalfSize = data.arenaHalfSize;
            this.enemyCount = data.enemyCount;
            this.victoryReward = data.victoryReward;
            this.resultsDelay = data.resultsDelay;
            this.randomSeed = data.randomSeed;
            this.playerArmor = data.playerArmor;
            this.criticalChance = data.criticalChance;
            this.criticalMultiplier = data.criticalMultiplier;
            this.obstacleLookupCellSize = data.obstacleLookupCellSize;
            this.obstaclePositions = Array.AsReadOnly((Vector3[])(data.obstaclePositions ?? Array.Empty<Vector3>()).Clone());
            this.obstacleScale = data.obstacleScale;
            this.enemyPathMaxWaypoints = data.enemyPathMaxWaypoints;
            this.Weapons = new WeaponSet(this.PlayerWeapon, this.EnemyTypes, data.availableWeapons);
            this.ArmedActorCapacity = 1;
            // Spawners may choose any registered enemy type for each finite-wave slot.
            foreach (var enemy in this.EnemyTypes) if (enemy.Weapon != null) {
                this.ArmedActorCapacity += this.enemyCount; break;
            }
            var lifetime = 0f; var interval = float.MaxValue; var hitCapacity = 0;
            foreach (var weapon in this.Weapons.Definitions) {
                lifetime = Math.Max(lifetime, weapon.Projectile.Lifetime);
                interval = Math.Min(interval, weapon.Interval);
                hitCapacity = Math.Max(hitCapacity, weapon.Projectile.HitDamageCapacity);
                this.MaxProjectileRadius = Math.Max(this.MaxProjectileRadius, weapon.Projectile.Radius);
            }
            // Cross-combine extremes: a fast weapon can be followed by a long-lived one.
            this.ProjectileCapacity = checked(((int)Math.Ceiling((double)lifetime / interval) + 2) * this.ArmedActorCapacity);
            if (this.ProjectileCapacity > 10000) throw new ArgumentException("Projectile lifetime / interval exceeds the session budget.");
            this.Navigation = new NavigationSettings(this);
            this.Arena = new ArenaSettings(this);
            this.PlayerAbilities = data.playerAbilities ?? new AbilityLoadout(data.playerAbility ?? throw new ArgumentNullException(nameof(data.playerAbility)));
            if (this.PlayerAbilities.Slots.Count == 0) throw new ArgumentException("The player requires a primary ability.");
            var definitions = new System.Collections.Generic.Dictionary<int, AbilityDefinition>();
            void Register(AbilityLoadout loadout) {
                foreach (var slot in loadout.Slots) {
                    if (definitions.TryGetValue(slot.Id, out var existing) && !ReferenceEquals(existing, slot))
                        throw new ArgumentException("Ability IDs must identify one shared definition.");
                    definitions[slot.Id] = slot;
                }
            }
            Register(this.PlayerAbilities);
            var enemySlots = 0;
            var enemyAbilityDamage = 0;
            foreach (var enemy in this.EnemyTypes) {
                Register(enemy.Abilities);
                enemySlots = Math.Max(enemySlots, enemy.Abilities.Slots.Count);
                enemyAbilityDamage = Math.Max(enemyAbilityDamage, enemy.Abilities.DamageCapacity(new AbilityTargetBudget(this.enemyCount + 1, 1)));
            }
            this.AbilityCastCapacity = checked(this.PlayerAbilities.Slots.Count + this.enemyCount * enemySlots);
            this.AbilityDefinitions = Array.AsReadOnly(new System.Collections.Generic.List<AbilityDefinition>(definitions.Values).ToArray());
            var periodic = false;
            foreach (var ability in this.AbilityDefinitions) foreach (var effect in ability.Effects) periodic |= effect.UsesPeriodicDamage;
            foreach (var weapon in this.Weapons.Definitions) {
                foreach (var hit in weapon.Projectile.Hits) periodic |= hit.UsesPeriodicDamage;
                foreach (var status in weapon.Projectile.SpawnStatuses) periodic |= status.PeriodicDamage != null;
            }
            this.DamageCapacity = CombatBufferBudget.Validate((long)this.enemyCount + (long)this.ProjectileCapacity * hitCapacity +
                this.PlayerAbilities.DamageCapacity(new AbilityTargetBudget(this.enemyCount + 1, this.enemyCount)) +
                (long)this.enemyCount * enemyAbilityDamage + (periodic ? (long)this.StatOwnerCapacity * 16 : 0), this.AbilityCastCapacity);
        }

        private static void Finite(float value, string name) {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }
        private static void NonNegative(float value, string name) {
            Finite(value, name);
            if (value < 0f) throw new ArgumentOutOfRangeException(name);
        }
        private static void Positive(float value, string name) {
            Finite(value, name);
            if (value <= 0f) throw new ArgumentOutOfRangeException(name);
        }
    }
}

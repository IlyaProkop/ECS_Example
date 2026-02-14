using UnityEngine;
using Game.Domain.Abilities;

namespace Game.Config {
    [CreateAssetMenu(menuName = "Game/Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject {
        [Header("Stat catalog")]
        public StatDefinitionAuthoring[] statDefinitions = StatDefinitionAuthoring.CreateDefaults();

        [Header("Player")]
        [Min(1f)] public float playerMaxHealth = 100f;
        [Min(0.1f)] public float playerMoveSpeed = 6f;
        [Min(0.1f)] public float playerRadius = 0.5f;
        [Header("Actor visual catalog")]
        public ActorVisualAuthoring[] actorVisuals = ActorVisualAuthoring.CreateDefaults();

        [Header("Projectile")]
        [Tooltip("Optional weapon asset. When empty, the legacy projectile fields below are used.")]
        public WeaponAsset playerWeapon;
        [Tooltip("Precompiled weapon choices available for runtime equipment changes.")]
        public WeaponAsset[] availableWeapons = System.Array.Empty<WeaponAsset>();
        [Min(0.1f)] public float projectileInterval = 0.3f;
        [Min(0.1f)] public float projectileSpeed = 16f;
        [Min(0.01f)] public float projectileRadius = 0.2f;
        [Min(1f)] public float projectileDamage = 25f;
        [Min(0.1f)] public float projectileLifetime = 4f;

        [Header("Enemy types (spawned in round-robin order)")]
        public EnemyAuthoring[] enemyTypes = { new EnemyAuthoring() };

        [Header("Enemy Pathfinding")]
        [Min(4)] public int enemyPathMaxWaypoints = 128;
        [Min(0.1f)] public float enemyPathCellSize = 0.6f;
        [Min(0.1f)] public float enemyPathRepathInterval = 0.6f;
        [Range(0f, 1f)] public float enemyPathRepathJitter = 0.2f;
        [Min(1)] public int enemyPathMaxRepathsPerFrame = 8;
        [Min(0.05f)] public float enemyPathWaypointReachDistance = 0.25f;
        [Min(0.05f)] public float enemyPathTargetMoveThreshold = 1f;
        [Min(0.5f)] public float enemyPathSearchPadding = 8f;
        [Min(4f)] public float enemyPathMaxSearchSize = 80f;
        [Min(128)] public int enemyPathMaxNodes = 8192;

        [Header("Spawning")]
        [Min(0.1f)] public float enemySpawnInterval = 0.8f;
        [Range(0f, 1f)] public float coinDropChance = 0.5f;

        [Header("Coin")]
        [Min(0.1f)] public float coinPickupRadius = 1f;
        [Min(0.1f)] public float coinRadius = 0.25f;

        [Header("Arena")]
        public Vector2 arenaHalfSize = new Vector2(14f, 10f);
        [Min(1)] public int enemyCount = 12;
        [Min(0)] public int victoryReward = 100;
        [Min(0f)] public float resultsDelay = 0.6f;
        public int randomSeed = 12345;

        [Header("Combat modifiers")]
        [Min(0f)] public float playerArmor = 10f;
        [Range(0f, 1f)] public float criticalChance = 0.15f;
        [Min(1f)] public float criticalMultiplier = 2f;

        [Header("Active ability")]
        public AbilityAsset playerAbility;
        public AbilityAsset[] additionalPlayerAbilities = System.Array.Empty<AbilityAsset>();
        public string AbilityDisplayName => this.playerAbility != null && !string.IsNullOrWhiteSpace(this.playerAbility.displayName)
            ? this.playerAbility.displayName : AbilityAsset.DefaultDisplayName;
        public string[] AbilityDisplayNames() {
            var names = new string[1 + (this.additionalPlayerAbilities?.Length ?? 0)];
            names[0] = this.AbilityDisplayName;
            for (var i = 1; i < names.Length; i++) names[i] = string.IsNullOrWhiteSpace(this.additionalPlayerAbilities[i].displayName)
                ? AbilityAsset.DefaultDisplayName : this.additionalPlayerAbilities[i].displayName;
            return names;
        }
        public float AbilityRingRadius => this.playerAbility != null ? this.playerAbility.ringRadius : BuiltInAbilities.ShockwaveRadius;

        public Vector3 PlayerSpawn => new Vector3(0f, 0f, -this.arenaHalfSize.y - 3f);

        public void Validate() {
            this.arenaHalfSize = new Vector2(Mathf.Max(10f, this.arenaHalfSize.x), Mathf.Max(8f, this.arenaHalfSize.y));
            this.enemyCount = Mathf.Clamp(this.enemyCount, 1, 10000);
            this.enemySpawnInterval = Mathf.Max(0.1f, this.enemySpawnInterval);
            this.projectileInterval = Mathf.Max(0.1f, this.projectileInterval);
            this.playerMaxHealth = Mathf.Max(1f, this.playerMaxHealth);
            this.playerRadius = Mathf.Max(0.1f, this.playerRadius);
            this.obstaclePositions ??= System.Array.Empty<Vector3>();
        }


        public Game.Domain.SimulationSettings CreateSimulationSettings(AbilityDefinition ability = null, Game.Domain.WeaponDefinition weapon = null) {
            var weapons = new WeaponAuthoringCache();
            var abilities = new AbilityAuthoringCache();
            var available = new Game.Domain.WeaponDefinition[this.availableWeapons?.Length ?? 0];
            for (var i = 0; i < available.Length; i++) available[i] = weapons.Resolve(this.availableWeapons[i])
                ?? throw new System.InvalidOperationException("Missing available weapon.");
            return new Game.Domain.SimulationSettings(new Game.Domain.SimulationSettingsData {
                statCatalog = this.CreateStatCatalog(),
                playerWeapon = weapon ?? weapons.Resolve(this.playerWeapon),
                availableWeapons = available,
                enemyTypes = this.CreateEnemyDefinitions(weapons, abilities),
                playerAbilities = abilities.Loadout(this.additionalPlayerAbilities, ability ?? (this.playerAbility != null ? abilities.Resolve(this.playerAbility) : BuiltInAbilities.CreateShockwave())),
                playerMaxHealth = this.playerMaxHealth,
                playerMoveSpeed = this.playerMoveSpeed,
                playerRadius = this.playerRadius,
                projectileInterval = this.projectileInterval,
                projectileSpeed = this.projectileSpeed,
                projectileRadius = this.projectileRadius,
                projectileDamage = this.projectileDamage,
                projectileLifetime = this.projectileLifetime,
                enemyPathCellSize = this.enemyPathCellSize,
                enemyPathRepathInterval = this.enemyPathRepathInterval,
                enemyPathRepathJitter = this.enemyPathRepathJitter,
                enemyPathMaxRepathsPerFrame = this.enemyPathMaxRepathsPerFrame,
                enemyPathWaypointReachDistance = this.enemyPathWaypointReachDistance,
                enemyPathTargetMoveThreshold = this.enemyPathTargetMoveThreshold,
                enemyPathSearchPadding = this.enemyPathSearchPadding,
                enemyPathMaxSearchSize = this.enemyPathMaxSearchSize,
                enemyPathMaxNodes = this.enemyPathMaxNodes,
                enemySpawnInterval = this.enemySpawnInterval,
                coinDropChance = this.coinDropChance,
                coinPickupRadius = this.coinPickupRadius,
                coinRadius = this.coinRadius,
                arenaHalfSize = this.arenaHalfSize,
                enemyCount = this.enemyCount,
                victoryReward = this.victoryReward,
                resultsDelay = this.resultsDelay,
                randomSeed = this.randomSeed,
                playerArmor = this.playerArmor,
                criticalChance = this.criticalChance,
                criticalMultiplier = this.criticalMultiplier,
                obstacleLookupCellSize = this.obstacleLookupCellSize,
                obstaclePositions = this.obstaclePositions,
                obstacleScale = this.obstacleScale,
                enemyPathMaxWaypoints = this.enemyPathMaxWaypoints
            });
        }

        private Game.Domain.EnemyDefinition[] CreateEnemyDefinitions(WeaponAuthoringCache weapons, AbilityAuthoringCache abilities) {
            if (this.enemyTypes == null) throw new System.InvalidOperationException("Missing enemy types.");
            var definitions = new Game.Domain.EnemyDefinition[this.enemyTypes.Length];
            for (var i = 0; i < definitions.Length; i++) {
                var enemy = this.enemyTypes[i] ?? throw new System.InvalidOperationException("Missing enemy definition.");
                definitions[i] = enemy.CreateDefinition(weapons.Resolve(enemy.weapon), abilities.Loadout(enemy.abilities));
            }
            return definitions;
        }

        private void OnValidate() => this.Validate();

        private Game.Domain.Stats.StatCatalog CreateStatCatalog() {
            if (this.statDefinitions == null) throw new System.InvalidOperationException("Missing stat catalog.");
            var definitions = new Game.Domain.Stats.StatDefinition[this.statDefinitions.Length];
            for (var i = 0; i < definitions.Length; i++) definitions[i] = this.statDefinitions[i]?.CreateDefinition()
                ?? throw new System.InvalidOperationException("Missing stat definition.");
            return new Game.Domain.Stats.StatCatalog(definitions);
        }

        [Header("Camera")]
        [Min(1f)] public float cameraHeight = 20f;
        [Min(1f)] public float cameraOrthographicSize = 12f;
        [Range(0f, 30f)] public float cameraFollowLerp = 10f;

        [Header("World UI")]
        [Min(0)] public int enemyWorldHpMaxLabels = 120;
        [Min(0f)] public float enemyWorldHpCullDistance = 45f;

        [Header("World")]
        [Min(10f)] public float groundSize = 80f;

        [Header("Optimization")]
        [Min(0.25f)] public float obstacleLookupCellSize = 3f;

        [Header("Obstacles")]
        public Vector3[] obstaclePositions = {
            new Vector3(6f, 0f, 6f),
            new Vector3(-8f, 0f, 4f),
            new Vector3(4f, 0f, -7f),
            new Vector3(-6f, 0f, -6f)
        };

        public Vector3 obstacleScale = new Vector3(3f, 2f, 3f);
    }
}

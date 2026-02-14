using System;
using Game.Domain.Abilities;
using Game.ECS.Abilities;
using Game.Domain;
using Game.Input;
using Game.Spatial;

namespace Game.ECS.Core {
    // Per-world services owned by GameSession. No scene objects or presentation callbacks.
    internal sealed class GameContext : IDisposable {
        public GameContext(SimulationSettings config, DamagePipeline damagePipeline = null) {
            this.Config = config ?? throw new ArgumentNullException(nameof(config));
            this.AbilityCasts = new FrameBuffer<AbilityCast>(config.AbilityCastCapacity);
            this.AbilityEvents = new FrameBuffer<AbilityCastEvent>(checked(config.AbilityCastCapacity * GameSession.MaxStepsPerFrame));
            this.DamageRequests = new FrameBuffer<DamageRequest>(config.DamageCapacity);
            this.Stats = new Stats.StatStorage(config.StatCatalog, config.StatOwnerCapacity, new Stats.StatusDamageSink(this.DamageRequests));
            this.Projectiles = new Projectiles.ProjectileRuntimeCatalog(this.Stats, Game.ECS.Projectiles.ProjectileModule.CreateRegistry(this.Stats, this.DamageRequests));
            this.Weapons = new Weapons.WeaponCatalog(config.Weapons, this.Projectiles);
            this.ProjectilePool = new Spawning.ProjectilePool(config.ProjectileCapacity, this.Stats);
            this.DamagePipeline = damagePipeline ?? new DamagePipeline(new CriticalDamageModifier(), new ArmorDamageModifier());
            this.EntityLookup = new EntityLookup();
            this.SpawnRandom = new Random(config.randomSeed);
            this.CombatRandom = new Random(unchecked(config.randomSeed + 104729));
            this.LootRandom = new Random(unchecked(config.randomSeed + 130363));
            this.NavigationRandom = new Random(unchecked(config.randomSeed + 155921));
            this.DamageApplied = new FrameBuffer<DamageApplied>(config.DamageCapacity);
            this.Paths = new Navigation.PathStorage(config.enemyCount, config.enemyPathMaxWaypoints);
            this.EnemySpatialIndex = new EnemySpatialIndex(config.enemyCount);
            this.ObstacleGridLookup = new ObstacleGridLookup(config, Math.Max(config.MaxEnemyRadius, config.MaxProjectileRadius));
            this.WeaponFired = new FrameBuffer<Weapons.WeaponFired>(config.ArmedActorCapacity);
        }
        internal FrameBuffer<AbilityCast> AbilityCasts { get; }
        internal FrameBuffer<AbilityCastEvent> AbilityEvents { get; }
        public SimulationSettings Config { get; }
        public Stats.StatStorage Stats { get; }
        public Projectiles.ProjectileRuntimeCatalog Projectiles { get; }
        public Weapons.WeaponCatalog Weapons { get; }
        public Spawning.ProjectilePool ProjectilePool { get; }
        public FrameBuffer<Weapons.WeaponFired> WeaponFired { get; }
        public EntityLookup EntityLookup { get; }
        public EnemySpatialIndex EnemySpatialIndex { get; }
        public ObstacleGridLookup ObstacleGridLookup { get; }
        public Navigation.PathStorage Paths { get; }
        public FrameBuffer<DamageRequest> DamageRequests { get; }
        public FrameBuffer<DamageApplied> DamageApplied { get; }
        public DamagePipeline DamagePipeline { get; }
        public Random SpawnRandom { get; }
        public Random CombatRandom { get; }
        public Random LootRandom { get; }
        public Random NavigationRandom { get; }
        internal SimulationInput InputState { get; } = new SimulationInput();

        public void Dispose() {
            this.AbilityCasts.Clear();
            this.AbilityEvents.Clear();
            this.DamageRequests.Clear();
            this.DamageApplied.Clear();
            this.WeaponFired.Clear();
            this.Projectiles.Dispose();
            this.ProjectilePool.Dispose();
            this.Stats.Dispose();
            this.ObstacleGridLookup.Dispose();
        }
    }
}

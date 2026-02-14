using System;
using System.Collections.Generic;
using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Spawning;
using Scellecs.Morpeh;

namespace Game.ECS.Core {
    // Composition only. Gameplay systems receive individual spawners, never this bundle.
    internal sealed class EntityFactory {
        private readonly World world;
        public PlayerSpawner Players { get; private set; }
        public IEnemySpawner Enemies { get; private set; }
        public IProjectileSpawner Projectiles { get; private set; }
        public ICoinSpawner Coins { get; private set; }

        public EntityFactory(World world, SimulationSettings config, Random navigationRandom, Stats.StatStorage stats, Projectiles.ProjectileRuntimeCatalog projectiles, Weapons.WeaponCatalog weapons, ProjectilePool pool = null) {
            this.world = world;
            using var steps = this.Initialize(config, navigationRandom, stats, projectiles, weapons, pool);
            while (steps.MoveNext()) { }
        }
        private EntityFactory(World world) { this.world = world; }
        internal static IEnumerator<object> CreateSteps(World world, SimulationSettings config, Random random,
            Stats.StatStorage stats, Projectiles.ProjectileRuntimeCatalog projectiles, Weapons.WeaponCatalog weapons, ProjectilePool pool, Action<EntityFactory> ready) {
            var factory = new EntityFactory(world);
            using var steps = factory.Initialize(config, random, stats, projectiles, weapons, pool);
            while (steps.MoveNext()) yield return null;
            ready(factory);
        }
        private IEnumerator<object> Initialize(SimulationSettings config, Random navigationRandom, Stats.StatStorage stats, Projectiles.ProjectileRuntimeCatalog projectiles, Weapons.WeaponCatalog weapons, ProjectilePool pool = null) {
            var actors = new ActorFactory(world, stats);
            yield return null;
            this.Players = new PlayerSpawner(world, actors, config, weapons);
            yield return null;
            this.Enemies = new EnemySpawner(world, actors, config, navigationRandom, weapons);
            yield return null;
            if (pool != null) {
                pool.Attach(this.world);
                while (!pool.PrepareBatch()) yield return null;
            }
            this.Projectiles = new ProjectileSpawner(world, actors, stats, projectiles, pool);
            yield return null;
            this.Coins = new CoinSpawner(world, actors, config.coinRadius, config.coinPickupRadius);
            yield return null;
        }

        public Entity CreateGameStateEntity() {
            var entity = this.world.CreateEntity();
            this.world.GetStash<GameStateComponent>().Set(entity, new GameStateComponent { phase = SessionPhase.Initialization });
            this.world.GetStash<BattleStatisticsComponent>().Add(entity);
            return entity;
        }
    }
}

using Game.ECS.Systems;
using Game.ECS.Abilities;
using Scellecs.Morpeh;

namespace Game.ECS.Core {
    internal sealed class GameplayInstaller {
        private readonly GameContext context;
        private readonly EntityFactory factory;
        public GameplayInstaller(GameContext context, EntityFactory factory) {
            this.context = context;
            this.factory = factory;
        }
        public void Install(World world, System.Func<ISystem, ISystem> decorate = null) {
            // Morpeh commits structural changes after every system. Buffer phases below
            // require no structural commits of their own and complete in this same tick.
            var systems = world.CreateSystemsGroup();
            void Add(ISystem system) => systems.AddSystem(decorate == null ? system : decorate(system));
            // 1. Advance time and input; periodic damage joins this step's damage queue.
            Add(new Stats.AdvanceStatusesSystem(this.context.Stats));
            Add(new PlayerInputSystem(this.context.EntityLookup, this.context.InputState));
            Add(new PlayerMovementSystem(this.context.Config, this.context.EntityLookup, this.context.ObstacleGridLookup));
            Add(new ArenaSessionSystem(this.context.Config, this.context.EntityLookup));
            Add(new EnemySpawnSystem(this.context.Config, this.context.EntityLookup, this.context.SpawnRandom, this.factory.Enemies, this.context.ObstacleGridLookup));
            // 2. Behaviour -> navigation -> steering/motion -> combat geometry.
            Add(new ChasePlayerSystem(this.context.EntityLookup));
            Add(new EnemyPathPlanningSystem(this.context.Config.Navigation, this.context.Config.Arena, this.context.Config.enemyCount, this.context.EntityLookup, this.context.ObstacleGridLookup, this.context.Paths, this.context.NavigationRandom));
            Add(new EnemyMovementSystem(this.context.Config.Navigation, this.context.Config.Arena, this.context.Config.enemyCount, this.context.EntityLookup, this.context.Paths, this.context.ObstacleGridLookup));
            Add(new EnemySpatialIndexSystem(this.context.EnemySpatialIndex)); // post-movement combat sample
            Add(new PlayerEnemySeparationSystem(this.context.Config, this.context.EntityLookup, this.context.EnemySpatialIndex, this.context.ObstacleGridLookup));
            // 3. Intent -> activation -> effects/facts. Facts consume weapon and ability buffers.
            Add(new Weapons.PlayerAttackIntentSystem(this.context.EntityLookup));
            Add(new Weapons.EnemyAttackIntentSystem());
            Add(new Weapons.WeaponFireSystem(this.context.EntityLookup, this.context.EnemySpatialIndex, this.context.Weapons,
                this.factory.Projectiles, this.context.WeaponFired));
            Add(new Weapons.WeaponStatisticsSystem(this.context.EntityLookup, this.context.WeaponFired));
            var abilities = AbilityModule.CreateRegistry(world, this.context.EnemySpatialIndex,
                this.context.ObstacleGridLookup, this.context.DamageRequests, this.context.Stats);
            AbilityModule.Install(Add, this.context.EntityLookup, this.context.Config.AbilityDefinitions,
                abilities, this.context.AbilityCasts, this.context.AbilityEvents);
            // 4. Publish ability stat changes before projectile/contact resolution.
            Add(new Stats.RecalculateStatsSystem(this.context.Stats));
            Add(new Projectiles.ProjectileMotionSystem(this.context.EntityLookup, this.context.Projectiles));
            Add(new ProjectileHitSystem(this.context.EntityLookup, this.context.EnemySpatialIndex, this.context.ObstacleGridLookup, this.context.Projectiles));
            Add(new EnemyContactDamageSystem(this.context.EntityLookup, this.context.EnemySpatialIndex, this.context.DamageRequests));
            Add(new CoinPickupSystem(this.context.EntityLookup));
            // 5. Resolve all damage, consume damage facts, then process deaths and outcome.
            Add(new ApplyDamageEventsSystem(this.context.EntityLookup, this.context.DamageRequests,
                this.context.DamageApplied, this.context.DamagePipeline, this.context.CombatRandom));
            Add(new CombatStatisticsSystem(this.context.EntityLookup, this.context.DamageApplied));
            Add(new EnemyDeathSystem(this.context.Config, this.context.EntityLookup, this.context.LootRandom, this.factory.Coins));
            Add(new ApplyGameOverSystem(this.context.Config, this.context.EntityLookup));
            // 6. Release status ownership and destroy marked entities last.
            Add(new Stats.CleanupStatusesSystem(this.context.Stats, this.context.EntityLookup));
            Add(new DestroyMarkedEntitiesSystem(this.context.Stats, this.context.ProjectilePool));
            world.AddSystemsGroup(0, systems);
        }
    }
}

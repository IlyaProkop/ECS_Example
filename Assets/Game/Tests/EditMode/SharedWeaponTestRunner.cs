using Game.ECS.Core;
using Game.ECS.Spawning;
using Game.ECS.Weapons;
using Scellecs.Morpeh;

namespace Game.Tests {
    internal static class SharedWeaponTestRunner {
        public static void Fire(World world, GameContext context, IProjectileSpawner spawner, float dt = 0f) {
            Run(world, new PlayerAttackIntentSystem(context.EntityLookup), dt);
            Run(world, new EnemyAttackIntentSystem(), dt);
            Run(world, new WeaponFireSystem(context.EntityLookup, context.EnemySpatialIndex, context.Weapons, spawner, context.WeaponFired), dt);
            Run(world, new WeaponStatisticsSystem(context.EntityLookup, context.WeaponFired), dt);
        }
        private static void Run(World world, ISystem system, float dt) {
            system.World = world; system.OnAwake(); world.Commit();
            try { system.OnUpdate(dt); world.Commit(); }
            finally { system.Dispose(); }
        }
    }
}

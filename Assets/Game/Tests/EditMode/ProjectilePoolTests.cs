using System;
using Game.Config;
using Game.Domain;
using Game.Domain.Stats;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.ECS.Projectiles;
using Game.ECS.Spawning;
using Game.ECS.Stats;
using Game.ECS.Systems;
using NUnit.Framework;
using Scellecs.Morpeh;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class ProjectilePoolTests {
        private GameConfig config;
        private GameContext context;
        private World world;
        private EntityFactory factory;
        private Entity player;
        private ProjectilePool Pool => this.context.ProjectilePool;
        private struct ExtraShotState : IComponent { public int value; }
        [SetUp] public void SetUp() {
            this.config = ScriptableObject.CreateInstance<GameConfig>(); this.config.obstaclePositions = Array.Empty<Vector3>();
            this.context = new GameContext(this.config.CreateSimulationSettings());
            this.world = World.Create("Projectile pool tests"); this.world.UpdateByUnity = false;
            this.Pool.Attach(this.world); this.Pool.RegisterReset<ExtraShotState>();
            this.factory = new EntityFactory(this.world, this.context.Config, this.context.NavigationRandom, this.context.Stats,
                this.context.Projectiles, this.context.Weapons, this.Pool);
            this.context.EntityLookup.gameState = this.factory.CreateGameStateEntity();
            this.player = this.context.EntityLookup.player = this.factory.Players.Spawn(Vector3.zero);
            this.world.GetStash<GameStateComponent>().Get(this.context.EntityLookup.gameState).phase = SessionPhase.Combat;
            this.world.Commit();
        }
        [TearDown] public void TearDown() { this.world.Dispose(); this.context.Dispose(); Object.DestroyImmediate(this.config); }
        private Entity Shot(float speed = 16f) => this.factory.Projectiles.Spawn(new ProjectileSpawn(Vector3.zero, Vector3.right,
            speed, 25f, .2f, 4f, this.world.GetStash<DamageSourceComponent>().Get(this.player), this.player.ID));
        private void Run(ISystem system, float dt = 0f) {
            system.World = this.world; system.OnAwake(); this.world.Commit();
            try { system.OnUpdate(dt); this.world.Commit(); } finally { system.Dispose(); }
        }
        private void Retire(Entity entity) {
            this.world.GetStash<DestroyTag>().Add(entity);
            this.Run(new DestroyMarkedEntitiesSystem(this.context.Stats, this.Pool));
        }
        private static StatusEffectDefinition Slow() => new StatusEffectDefinition(72, 3f, StatusStacking.Refresh, 1,
            new StatModifierDefinition(StatIds.MoveSpeed, StatOperation.Multiply, .5f));

        [Test] public void ReturnedStorageIsReusedWithNewActorIdentityAndInvalidatesOldLeases() {
            var shot = this.Shot(); var oldId = this.world.GetStash<ActorComponent>().Get(shot).id;
            var lease = this.Pool.Capture(shot); this.Retire(shot);
            Assert.That(shot.IsNullOrDisposed(), Is.False);
            Assert.That(this.Pool.TryResolve(lease, out _), Is.False); Assert.That(this.Pool.TryReturn(lease), Is.False);
            var replacement = this.Shot(); Assert.That(replacement, Is.SameAs(shot));
            Assert.That(this.world.GetStash<ActorComponent>().Get(replacement).id, Is.GreaterThan(oldId));
            Assert.That(this.Pool.TryReturn(lease), Is.False, "A stale lease must not return the new shot.");
            Assert.That(this.Pool.TryResolve(this.Pool.Capture(replacement), out var resolved), Is.True);
            Assert.That(resolved, Is.SameAs(replacement)); Assert.That(this.Pool.ActiveCount, Is.EqualTo(1));
        }
        [Test] public void ReturnClearsStatusSourceTargetMotionAndRegisteredExtensionState() {
            var shot = this.Shot(); var status = this.context.Stats.Compile(Slow());
            var handle = this.context.Stats.Apply(shot, new EffectSource(this.player.ID, 72), status);
            this.context.Stats.SetBase(shot, StatIds.DamageMultiplier, 9f); this.context.Stats.RecalculateDirty();
            this.world.GetStash<ExtraShotState>().Set(shot, new ExtraShotState { value = 99 });
            this.world.GetStash<HealthComponent>().Set(shot, new HealthComponent { current = 1f, max = 1f });
            this.Retire(shot);
            Assert.That(this.context.Stats.ActiveCount, Is.Zero); Assert.That(this.context.Stats.CanReceive(shot), Is.False);
            Assert.That(this.world.GetStash<ProjectilePayloadComponent>().Get(shot), Is.EqualTo(default(ProjectilePayloadComponent)));
            Assert.That(this.world.GetStash<DamageSourceComponent>().Get(shot), Is.EqualTo(default(DamageSourceComponent)));
            Assert.That(this.world.GetStash<ProjectileComponent>().Get(shot), Is.EqualTo(default(ProjectileComponent)));
            var next = this.factory.Projectiles.Spawn(new ProjectileSpawn(Vector3.forward, Vector3.left, 10f, 7f, .3f, 1f, default));
            Assert.That(next, Is.SameAs(shot)); Assert.That(this.context.Stats.Remove(handle), Is.False);
            Assert.That(this.world.GetStash<ExtraShotState>().Has(next), Is.False); Assert.That(this.world.GetStash<HealthComponent>().Has(next), Is.False);
            Assert.That(this.context.Stats.GetFinal(next, StatIds.MoveSpeed), Is.EqualTo(10f));
            Assert.That(this.context.Stats.GetFinal(next, StatIds.DamageMultiplier), Is.EqualTo(1f));
            Assert.That(this.world.GetStash<ProjectilePayloadComponent>().Get(next).target, Is.EqualTo(default(Scellecs.Morpeh.EntityId)));
            Assert.That(this.world.GetStash<DamageSourceComponent>().Get(next), Is.EqualTo(default(DamageSourceComponent)));
            this.Run(new ProjectileMotionSystem(this.context.EntityLookup, this.context.Projectiles), .1f);
            Assert.That(this.world.GetStash<PositionComponent>().Get(next).value, Is.EqualTo(Vector3.forward + Vector3.left));
        }
        [Test] public void InactiveEntitiesAreAbsentFromSnapshotsAreaStatusesAndProjectileFilters() {
            var shot = this.Shot(); this.Retire(shot);
            var reader = new SessionReader(this.world, this.context.EntityLookup, this.context.Config.enemyCount, this.context.Stats);
            var views = new ActorView[this.context.Config.ViewCapacity]; this.world.Commit();
            Assert.That(reader.CopyActors(views), Is.EqualTo(1)); Assert.That(views[0].kind, Is.EqualTo(ActorKind.Player));
            Assert.That(this.world.Filter.With<ProjectileTag>().Build().GetLengthSlow(), Is.Zero);
            using var area = new Game.ECS.Abilities.AreaStatusEffect(new AreaStatusDefinition(10f, StatusTargets.Projectile, Slow()),
                this.world, this.context.Stats, this.context.ObstacleGridLookup);
            this.world.Commit();
            area.Execute(new Game.ECS.Abilities.AbilityCast(this.player, 72, Vector3.zero, default));
            Assert.That(this.context.Stats.ActiveCount, Is.Zero);
        }
        [Test] public void CapacityFailureAndFailedInitializationDoNotLosePoolSlots() {
            var shots = new Entity[this.Pool.Capacity];
            for (var i = 0; i < shots.Length; i++) shots[i] = this.Shot();
            Assert.Throws<InvalidOperationException>(() => this.Shot());
            Assert.That(this.Pool.CreatedCount, Is.EqualTo(shots.Length));
            var oldId = this.world.GetStash<ActorComponent>().Get(shots[shots.Length - 1]).id;
            this.Retire(shots[0]); var next = this.Shot();
            Assert.That(this.world.GetStash<ActorComponent>().Get(next).id, Is.EqualTo(oldId + 1));
            this.Retire(next);
            Assert.Throws<ArgumentOutOfRangeException>(() => this.Shot(float.NaN));
            Assert.That(this.Pool.ActiveCount, Is.EqualTo(shots.Length - 1));
            Assert.DoesNotThrow(() => this.Shot());
        }
        [Test] public void DuplicateReturnAndForeignEntitiesCannotCorruptTheFreeList() {
            var shot = this.Shot(); var lease = this.Pool.Capture(shot);
            Assert.That(this.Pool.TryReturn(lease), Is.True); Assert.That(this.Pool.TryReturn(lease), Is.False);
            Assert.That(this.Pool.TryRecycle(shot), Is.True); Assert.That(this.Pool.ActiveCount, Is.Zero);
            using var foreign = World.Create("Foreign projectile pool"); var actor = foreign.CreateEntity();
            Assert.That(this.Pool.TryRecycle(actor), Is.False); Assert.That(this.Pool.TryRecycle(this.player), Is.False);
            this.Pool.Dispose(); this.Pool.Dispose();
            Assert.Throws<ObjectDisposedException>(() => this.Pool.Rent());
            Assert.Throws<ObjectDisposedException>(() => this.Pool.TryReturn(lease));
        }
        [Test] public void ReturningBulletDoesNotRemoveItsBurnFromAnotherActor() {
            var enemy = this.factory.Enemies.Spawn(Vector3.right, this.context.Config.EnemyTypes[0]);
            var burn = new StatusEffectDefinition(73, 1f, StatusStacking.Refresh, 1, new PeriodicDamageDefinition(.5f, 5f));
            var status = this.context.Stats.Compile(burn); var shot = this.Shot();
            var source = this.world.GetStash<DamageSourceComponent>().Get(shot);
            this.context.Stats.TryApply(enemy, new EffectSource(source.owner, 73), status, source, out _);
            this.Retire(shot); this.Shot(); this.context.Stats.Advance(.5f);
            Assert.That(this.context.DamageRequests.Count, Is.EqualTo(1));
            Assert.That(this.context.DamageRequests.Items[0].target, Is.SameAs(enemy));
            Assert.That(this.context.DamageRequests.Items[0].source.owner, Is.EqualTo(this.player.ID));
        }
        [Test] public void ReusedSlotCanChangeTemplateVisualAndTeamWithoutKeepingSpawnEffects() {
            var definition = new ProjectileDefinition(10f, 7f, .3f, 1f, new HomingProjectileMotion(),
                new ProjectileHitDefinition[] { new DirectProjectileDamage() }, new[] { Slow() }, 99);
            var index = this.context.Projectiles.Compile(definition);
            var old = this.factory.Projectiles.Spawn(new ProjectileSpawn(Vector3.zero, Vector3.right, 10f, 7f, .3f, 1f,
                new DamageSourceComponent { team = Team.Enemy }, this.player.ID, index));
            Assert.That(this.context.Stats.GetFinal(old, StatIds.MoveSpeed), Is.EqualTo(5f));
            Assert.That(this.world.GetStash<ActorComponent>().Get(old).visualId, Is.EqualTo(99));
            this.Retire(old); var current = this.Shot(); Assert.That(current, Is.SameAs(old));
            Assert.That(this.world.GetStash<ProjectilePayloadComponent>().Get(current).definitionIndex, Is.Zero);
            Assert.That(this.world.GetStash<ActorComponent>().Get(current).visualId, Is.EqualTo(ActorVisualIds.Projectile));
            Assert.That(this.world.GetStash<DamageSourceComponent>().Get(current).team, Is.EqualTo(Team.Player));
            Assert.That(this.context.Stats.GetFinal(current, StatIds.MoveSpeed), Is.EqualTo(16f));
            Assert.That(this.context.Stats.ActiveCount, Is.Zero);
        }
        [Test] public void HomingDoesNotFollowAReusedLifetimeOfAPooledTarget() {
            var target = this.Shot(); this.world.GetStash<PositionComponent>().Get(target).value = Vector3.right * 5f;
            this.world.GetStash<HealthComponent>().Set(target, new HealthComponent { current = 1f });
            var index = this.context.Projectiles.Compile(new ProjectileDefinition(10f, 1f, .1f, 2f, new HomingProjectileMotion(),
                new ProjectileHitDefinition[] { new DirectProjectileDamage() }));
            var seeker = this.factory.Projectiles.Spawn(new ProjectileSpawn(Vector3.zero, Vector3.right, 10f, 1f, .1f, 2f, default, target.ID, index));
            this.Retire(target); var replacement = this.Shot(); Assert.That(replacement, Is.SameAs(target));
            this.world.GetStash<PositionComponent>().Get(replacement).value = Vector3.forward * 5f;
            this.world.GetStash<HealthComponent>().Set(replacement, new HealthComponent { current = 1f });
            this.Run(new ProjectileMotionSystem(this.context.EntityLookup, this.context.Projectiles), .1f);
            Assert.That(this.world.GetStash<PositionComponent>().Get(seeker).value, Is.EqualTo(Vector3.right));
        }
        [TestCase(16)] [TestCase(128)]
        public void WarmPoolRemovesPerShotManagedAllocationsComparedWithDirectCreation(int batch) {
            var direct = Game.Diagnostics.ProjectileLifecycleBenchmark.Measure(batch, false);
            var pooled = Game.Diagnostics.ProjectileLifecycleBenchmark.Measure(batch, true);
            TestContext.Out.WriteLine("PROJECTILE_LIFECYCLE " + JsonUtility.ToJson(direct));
            TestContext.Out.WriteLine("PROJECTILE_LIFECYCLE " + JsonUtility.ToJson(pooled));
            Assert.That(direct.allocationCounterSupported && pooled.allocationCounterSupported, Is.True);
            Assert.That(direct.allocationEvents, Is.GreaterThan(0)); Assert.That(pooled.allocationEvents, Is.Zero);
        }
    }
}

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
    public sealed class ProjectileModuleTests {
        private World world;
        private GameContext context;
        private EntityFactory factory;
        private GameConfig config;
        private Entity player, enemy;
        private ProjectileDefinition definition;
        [SetUp] public void SetUp() {
            this.world = null; this.context = null;
            this.config = ScriptableObject.CreateInstance<GameConfig>();
            this.config.enemyCount = 2; this.config.criticalChance = 0f;
            this.config.obstaclePositions = Array.Empty<Vector3>();
        }
        [TearDown] public void TearDown() {
            this.world?.Dispose(); this.context?.Dispose(); Object.DestroyImmediate(this.config);
        }
        private void Create(ProjectileMotionDefinition motion = null, ProjectileHitDefinition[] hits = null,
            StatusEffectDefinition[] statuses = null) {
            this.definition = new ProjectileDefinition(10f, 25f, .2f, 4f, motion ?? new StraightProjectileMotion(),
                hits ?? new ProjectileHitDefinition[] { new DirectProjectileDamage() }, statuses);
            this.context = new GameContext(this.config.CreateSimulationSettings(weapon: new WeaponDefinition(.3f, this.definition)));
            this.world = World.Create("Projectile extension tests"); this.world.UpdateByUnity = false;
            this.factory = new EntityFactory(this.world, this.context.Config, this.context.NavigationRandom, this.context.Stats, this.context.Projectiles, this.context.Weapons);
            this.context.EntityLookup.gameState = this.factory.CreateGameStateEntity();
            this.player = this.context.EntityLookup.player = this.factory.Players.Spawn(Vector3.zero);
            this.enemy = this.factory.Enemies.Spawn(Vector3.right * 10f, this.context.Config.EnemyTypes[0]);
            this.world.GetStash<GameStateComponent>().Get(this.context.EntityLookup.gameState).phase = SessionPhase.Combat;
            this.world.Commit();
        }
        private Entity Shot() => this.factory.Projectiles.Spawn(new ProjectileSpawn(Vector3.zero, Vector3.right, 10f, 25f, .2f, 4f,
            this.world.GetStash<DamageSourceComponent>().Get(this.player), this.enemy.ID));
        private void Run(ISystem system, float dt = 0f) {
            system.World = this.world; system.OnAwake(); this.world.Commit(); system.OnUpdate(dt); this.world.Commit(); system.Dispose();
        }
        private void Move(float dt) => this.Run(new ProjectileMotionSystem(this.context.EntityLookup, this.context.Projectiles), dt);
        private void Hit() {
            this.Run(new EnemySpatialIndexSystem(this.context.EnemySpatialIndex));
            this.Run(new ProjectileHitSystem(this.context.EntityLookup, this.context.EnemySpatialIndex, this.context.ObstacleGridLookup, this.context.Projectiles));
        }
        private void Damage() => this.Run(new ApplyDamageEventsSystem(this.context.EntityLookup, this.context.DamageRequests,
            this.context.DamageApplied, this.context.DamagePipeline, this.context.CombatRandom));
        private void Advance(float dt) { this.context.Stats.Advance(dt); this.context.Stats.RecalculateDirty(); }
        private static StatusEffectDefinition Slow() => new StatusEffectDefinition(21, .5f, StatusStacking.Refresh, 1,
            new StatModifierDefinition(StatIds.MoveSpeed, StatOperation.Multiply, .5f));

        [TestCase(false)] [TestCase(true)]
        public void MotionSelectionControlsWhetherTheShotTracksALaterTargetPosition(bool homing) {
            this.Create(homing ? new HomingProjectileMotion() : new StraightProjectileMotion());
            var shot = this.Shot(); this.Move(.1f);
            this.world.GetStash<PositionComponent>().Get(this.enemy).value = Vector3.forward * 10f;
            this.Move(.1f);
            var position = this.world.GetStash<PositionComponent>().Get(shot).value;
            if (homing) { Assert.That(position.z, Is.GreaterThan(.9f)); Assert.That(position.x, Is.LessThan(1f)); }
            else Assert.That(position, Is.EqualTo(Vector3.right * 2f));
        }
        [Test]
        public void DeadHomingTargetDoesNotRetargetOrFollowAReusedEntityId() {
            this.Create(new HomingProjectileMotion()); var shot = this.Shot();
            this.context.Stats.Release(this.enemy); this.world.RemoveEntity(this.enemy); this.world.Commit();
            var replacement = this.factory.Enemies.Spawn(Vector3.forward * 10f, this.context.Config.EnemyTypes[0]);
            this.Move(.1f);
            Assert.That(this.world.GetStash<PositionComponent>().Get(shot).value, Is.EqualTo(Vector3.right));
            Assert.That(this.world.GetStash<HealthComponent>().Get(replacement).current, Is.GreaterThan(0f));
        }
        private sealed class LeftMotion : ProjectileMotionDefinition {
            public override Vector3 Direction(Vector3 position, Vector3 direction, Vector3 target, bool alive) => Vector3.left;
        }
        [Test]
        public void NewMotionRequiresNoChangeInShootingOrCollisionSystems() {
            this.Create(new LeftMotion()); var shot = this.Shot(); this.Move(.1f);
            Assert.That(this.world.GetStash<PositionComponent>().Get(shot).value, Is.EqualTo(Vector3.left));
        }
        [Test]
        public void OneStatusDefinitionCanAffectPlayerEnemyAndHealthlessProjectile() {
            this.Create(statuses: new[] { Slow() }); var shot = this.Shot();
            var status = this.context.Stats.Compile(Slow()); var origin = new EffectSource(this.player.ID, 21);
            this.context.Stats.Apply(this.player, origin, status); this.context.Stats.Apply(this.enemy, origin, status);
            this.context.Stats.RecalculateDirty(); this.Move(.1f);
            Assert.That(this.context.Stats.GetFinal(this.player, StatIds.MoveSpeed), Is.EqualTo(this.config.playerMoveSpeed * .5f));
            Assert.That(this.context.Stats.GetFinal(this.enemy, StatIds.MoveSpeed), Is.EqualTo(this.config.enemyTypes[0].moveSpeed * .5f));
            Assert.That(this.world.GetStash<PositionComponent>().Get(shot).value.x, Is.EqualTo(.5f));
            this.Advance(.5f); this.Move(.1f);
            Assert.That(this.world.GetStash<PositionComponent>().Get(shot).value.x, Is.EqualTo(1.5f));
            Assert.That(this.context.Stats.ActiveCount, Is.Zero);
        }
        [Test]
        public void IncendiaryHitPersistsAfterShotAndOwnerDestructionAndUsesTheDamagePipeline() {
            var burn = new StatusEffectDefinition(22, 1f, StatusStacking.Refresh, 1, new PeriodicDamageDefinition(.5f, 10f));
            this.Create(hits: new ProjectileHitDefinition[] { new DirectProjectileDamage(), new ProjectileStatusHit(burn) });
            this.context.Stats.SetBase(this.enemy, StatIds.Armor, 100f); this.context.Stats.RecalculateDirty();
            var original = this.world.GetStash<HealthComponent>().Get(this.enemy).current;
            var shot = this.Shot(); this.Move(1f); this.Hit(); this.Damage();
            Assert.That(this.world.GetStash<HealthComponent>().Get(this.enemy).current, Is.EqualTo(original - 12.5f));
            this.Run(new DestroyMarkedEntitiesSystem(this.context.Stats));
            this.context.Stats.Release(this.player); this.world.RemoveEntity(this.player); this.world.Commit();
            Assert.That(shot.IsNullOrDisposed(), Is.True);
            this.Advance(.5f); Assert.That(this.context.DamageRequests.Items[0].source.team, Is.EqualTo(Team.Player));
            this.Damage(); this.Advance(.5f); this.Damage();
            Assert.That(this.world.GetStash<HealthComponent>().Get(this.enemy).current, Is.EqualTo(original - 22.5f));
            Assert.That(this.context.Stats.ActiveCount, Is.Zero);
        }
        [Test]
        public void RefreshPreservesBurnTickPhaseAndDeathCancelsFutureTicks() {
            var burn = new StatusEffectDefinition(22, 1f, StatusStacking.Refresh, 1, new PeriodicDamageDefinition(.5f, 10f));
            this.Create(hits: new ProjectileHitDefinition[] { new ProjectileStatusHit(burn) });
            var status = this.context.Stats.Compile(burn); var source = new EffectSource(this.player.ID, 22);
            var attribution = this.world.GetStash<DamageSourceComponent>().Get(this.player);
            this.context.Stats.TryApply(this.enemy, source, status, attribution, out var old);
            this.Advance(.3f); this.context.Stats.TryApply(this.enemy, source, status, attribution, out var current);
            Assert.That(this.context.Stats.Remove(old), Is.False);
            this.Advance(.2f); Assert.That(this.context.DamageRequests.Count, Is.EqualTo(1)); this.Damage();
            this.world.GetStash<HealthComponent>().Get(this.enemy).current = 0f;
            this.Advance(1f); Assert.That(this.context.DamageRequests.Count, Is.Zero);
            Assert.That(this.context.Stats.Remove(current), Is.False);
        }
        [Test]
        public void ProjectileDamageBuffAppliesIndependentlyOfTheOwner() {
            var buff = new StatusEffectDefinition(23, 3f, StatusStacking.Refresh, 1,
                new StatModifierDefinition(StatIds.DamageMultiplier, StatOperation.Multiply, 2f));
            this.Create(statuses: new[] { buff }); this.Shot(); this.Move(1f); this.Hit();
            Assert.That(this.context.DamageRequests.Items[0].value, Is.EqualTo(50f));
        }
        [Test]
        public void OwnerDamageBuffIsSnapshottedAtShotCreation() {
            this.Create();
            var buff = this.context.Stats.Compile(new StatusEffectDefinition(23, 3f, StatusStacking.Refresh, 1,
                new StatModifierDefinition(StatIds.DamageMultiplier, StatOperation.Multiply, 2f)));
            var handle = this.context.Stats.Apply(this.player, new EffectSource(this.player.ID, 23), buff);
            this.context.Stats.RecalculateDirty();
            this.world.GetStash<CombatInputComponent>().Get(this.player).attackHeld = true;
            this.Run(new EnemySpatialIndexSystem(this.context.EnemySpatialIndex));
            SharedWeaponTestRunner.Fire(this.world, this.context, this.factory.Projectiles);
            this.context.Stats.Remove(handle); this.context.Stats.RecalculateDirty();
            this.Move(1f); this.Hit();
            Assert.That(this.context.DamageRequests.Items[0].value, Is.EqualTo(50f));
        }
        [Test]
        public void EnemyShotCanHitThePlayerAndCannotHitAnEnemy() {
            this.Create();
            this.factory.Projectiles.Spawn(new ProjectileSpawn(Vector3.right * 10f, Vector3.left, 10f, 25f, .2f, 4f,
                this.world.GetStash<DamageSourceComponent>().Get(this.enemy), this.player.ID));
            this.Move(1f); this.Hit();
            Assert.That(this.context.DamageRequests.Count, Is.EqualTo(1));
            Assert.That(this.context.DamageRequests.Items[0].target, Is.SameAs(this.player));
        }
        [Test]
        public void RepeatedProjectileDestructionReusesStatRowsWithoutInheritingStatusHandles() {
            this.Create(); var status = this.context.Stats.Compile(Slow()); var source = new EffectSource(this.player.ID, 21);
            for (var i = 0; i < 200; i++) {
                var shot = this.Shot(); var handle = this.context.Stats.Apply(shot, source, status);
                this.world.GetStash<DestroyTag>().Add(shot); this.Run(new DestroyMarkedEntitiesSystem(this.context.Stats));
                var next = this.Shot(); this.Advance(0f);
                Assert.That(this.context.Stats.Remove(handle), Is.False);
                Assert.That(this.context.Stats.GetFinal(next, StatIds.MoveSpeed), Is.EqualTo(10f));
                this.world.GetStash<DestroyTag>().Add(next); this.Run(new DestroyMarkedEntitiesSystem(this.context.Stats));
            }
            Assert.That(this.context.Stats.ActiveCount, Is.Zero);
        }
        [Test]
        public void AreaStatusCanSelectEnemyAndProjectileWithoutChangingThePlayer() {
            this.Create(); var shot = this.Shot();
            var definition = new AreaStatusDefinition(20f, StatusTargets.Enemy | StatusTargets.Projectile, Slow());
            using var effect = new Game.ECS.Abilities.AreaStatusEffect(definition, this.world, this.context.Stats, this.context.ObstacleGridLookup);
            this.world.Commit();
            effect.Execute(new Game.ECS.Abilities.AbilityCast(this.player, 50, Vector3.zero, this.world.GetStash<DamageSourceComponent>().Get(this.player)));
            this.context.Stats.RecalculateDirty();
            Assert.That(this.context.Stats.GetFinal(this.player, StatIds.MoveSpeed), Is.EqualTo(this.config.playerMoveSpeed));
            Assert.That(this.context.Stats.GetFinal(this.enemy, StatIds.MoveSpeed), Is.EqualTo(this.config.enemyTypes[0].moveSpeed * .5f));
            Assert.That(this.context.Stats.GetFinal(shot, StatIds.MoveSpeed), Is.EqualTo(5f));
        }
        [Test]
        public void WeaponAssetSnapshotsDataAndOverridesLegacyFields() {
            var weapon = ScriptableObject.CreateInstance<WeaponAsset>();
            var projectile = ScriptableObject.CreateInstance<ProjectileAsset>();
            var damage = ScriptableObject.CreateInstance<DirectDamageHitAsset>();
            try {
                projectile.hitEffects = new ProjectileHitAsset[] { damage }; projectile.motion = ProjectileMotionMode.Homing;
                projectile.speed = 30f; weapon.projectile = projectile; weapon.interval = .2f; this.config.playerWeapon = weapon;
                var settings = this.config.CreateSimulationSettings();
                projectile.speed = 90f; projectile.hitEffects[0] = null; weapon.interval = 1f;
                Assert.That(settings.PlayerWeapon.Interval, Is.EqualTo(.2f));
                Assert.That(settings.projectileSpeed, Is.EqualTo(30f));
                Assert.That(settings.PlayerWeapon.Projectile.Motion, Is.TypeOf<HomingProjectileMotion>());
                Assert.That(settings.PlayerWeapon.Projectile.Hits.Count, Is.EqualTo(1));
            } finally { Object.DestroyImmediate(weapon); Object.DestroyImmediate(projectile); Object.DestroyImmediate(damage); }
        }
        [Test]
        public void DefinitionCopiesEffectArraysAndRejectsConflictingPeriodicIds() {
            this.Create(); var hit = new DirectProjectileDamage(); var hits = new ProjectileHitDefinition[] { hit };
            var definition = new ProjectileDefinition(10f, 1f, .1f, 1f, new StraightProjectileMotion(), hits);
            hits[0] = null; Assert.That(definition.Hits[0], Is.SameAs(hit));
            this.context.Stats.Compile(new StatusEffectDefinition(30, 1f, StatusStacking.Refresh, 1, new PeriodicDamageDefinition(.5f, 2f)));
            Assert.Throws<ArgumentException>(() => this.context.Stats.Compile(new StatusEffectDefinition(30, 1f, StatusStacking.Refresh, 1,
                new PeriodicDamageDefinition(.5f, 3f))));
        }
    }
}

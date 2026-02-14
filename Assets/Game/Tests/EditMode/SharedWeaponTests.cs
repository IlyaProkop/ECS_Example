using System;
using System.Collections.Generic;
using System.Linq;
using Game.Config;
using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.ECS.Projectiles;
using Game.ECS.Spawning;
using Game.ECS.Systems;
using Game.ECS.Weapons;
using Game.Input;
using NUnit.Framework;
using Scellecs.Morpeh;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class SharedWeaponTests {
        private readonly List<Object> assets = new();
        private GameConfig config;
        private WeaponAsset fire, homing;
        private World world;
        private GameContext context;
        private EntityFactory factory;
        private Entity player, enemy;
        private T Asset<T>() where T : ScriptableObject {
            var asset = ScriptableObject.CreateInstance<T>(); this.assets.Add(asset); return asset;
        }
        [SetUp] public void SetUp() {
            this.world = null; this.context = null;
            this.config = this.Asset<GameConfig>(); this.config.enemyCount = 3;
            this.config.obstaclePositions = Array.Empty<Vector3>(); this.config.criticalChance = 0f;
            var damage = this.Asset<DirectDamageHitAsset>();
            var burn = this.Asset<StatusEffectAsset>(); burn.id = 31; burn.duration = 1f;
            burn.periodicDamage = true; burn.tickInterval = .5f; burn.tickDamage = 5f;
            var ignite = this.Asset<StatusHitAsset>(); ignite.status = burn;
            var fireShot = this.Asset<ProjectileAsset>(); fireShot.speed = 10f;
            fireShot.hitEffects = new ProjectileHitAsset[] { damage, ignite };
            var homingShot = this.Asset<ProjectileAsset>(); homingShot.speed = 10f;
            homingShot.motion = ProjectileMotionMode.Homing; homingShot.hitEffects = new ProjectileHitAsset[] { damage };
            this.fire = this.Asset<WeaponAsset>(); this.fire.id = 3; this.fire.interval = .5f; this.fire.projectile = fireShot;
            this.homing = this.Asset<WeaponAsset>(); this.homing.id = 2; this.homing.interval = .1f; this.homing.projectile = homingShot;
            this.config.playerWeapon = this.fire; this.config.availableWeapons = new[] { this.homing };
            this.config.enemyTypes[0].weapon = this.fire;
            this.config.enemyTypes[0].maxHealth = 1000f; this.config.enemyTypes[0].damagePerSecond = 0f;
        }
        [TearDown] public void TearDown() {
            this.world?.Dispose(); this.context?.Dispose();
            foreach (var asset in this.assets) Object.DestroyImmediate(asset);
            this.assets.Clear();
        }
        private void Create(bool armed = true, bool pooled = false) {
            if (!armed) this.config.enemyTypes[0].weapon = null;
            this.context = new GameContext(this.config.CreateSimulationSettings());
            this.world = World.Create("Shared weapons"); this.world.UpdateByUnity = false;
            this.factory = new EntityFactory(this.world, this.context.Config, this.context.NavigationRandom, this.context.Stats, this.context.Projectiles, this.context.Weapons,
                pooled ? this.context.ProjectilePool : null);
            this.context.EntityLookup.gameState = this.factory.CreateGameStateEntity();
            this.player = this.context.EntityLookup.player = this.factory.Players.Spawn(Vector3.zero);
            this.enemy = this.factory.Enemies.Spawn(Vector3.right * 5f, this.context.Config.EnemyTypes[0]);
            this.world.GetStash<GameStateComponent>().Get(this.context.EntityLookup.gameState).phase = SessionPhase.Combat;
            this.world.GetStash<CombatInputComponent>().Get(this.player).attackHeld = true;
            this.Run(new EnemySpatialIndexSystem(this.context.EnemySpatialIndex));
        }
        private void Run(ISystem system, float dt = 0f) {
            system.World = this.world; system.OnAwake(); this.world.Commit();
            try { system.OnUpdate(dt); this.world.Commit(); } finally { system.Dispose(); }
        }
        private void Fire(float dt = 0f, IProjectileSpawner spawner = null) =>
            SharedWeaponTestRunner.Fire(this.world, this.context, spawner ?? this.factory.Projectiles, dt);
        private Entity[] Shots() {
            var shots = new List<Entity>();
            foreach (var shot in this.world.Filter.With<ProjectileTag>().Without<DestroyTag>().Build()) shots.Add(shot);
            return shots.ToArray();
        }
        private bool Equip(Entity actor, int id) => WeaponEquipment.TryChange(this.world, actor, this.context.Weapons, id);
        private AttackTargeting Targeting() {
            var targeting = new AttackTargeting(this.world, this.context.EntityLookup, this.context.EnemySpatialIndex);
            this.world.Commit(); return targeting;
        }

        [Test] public void PlayerAndEnemyShareFireDefinitionButOwnCooldownAndDamageAttribution() {
            this.Create();
            Assert.That(this.context.Config.EnemyTypes[0].Weapon, Is.SameAs(this.context.Config.PlayerWeapon));
            this.Fire(); var shots = this.Shots(); Assert.That(shots.Length, Is.EqualTo(2));
            var sources = this.world.GetStash<DamageSourceComponent>();
            var payloads = this.world.GetStash<ProjectilePayloadComponent>();
            var heroShot = shots.Single(s => sources.Get(s).team == Team.Player);
            var enemyShot = shots.Single(s => sources.Get(s).team == Team.Enemy);
            Assert.That(payloads.Get(heroShot).definitionIndex, Is.EqualTo(payloads.Get(enemyShot).definitionIndex));
            Assert.That(payloads.Get(heroShot).target, Is.EqualTo(this.enemy.ID));
            Assert.That(payloads.Get(enemyShot).target, Is.EqualTo(this.player.ID));
            Assert.That(sources.Get(heroShot).owner, Is.EqualTo(this.player.ID));
            Assert.That(sources.Get(enemyShot).owner, Is.EqualTo(this.enemy.ID));
            this.world.GetStash<WeaponComponent>().Get(this.player).cooldown = .8f;
            this.Fire(.5f); Assert.That(this.Shots().Length, Is.EqualTo(3));
            Assert.That(this.world.GetStash<BattleStatisticsComponent>().Get(this.context.EntityLookup.gameState).value.attacks, Is.EqualTo(1));
            this.Run(new ProjectileMotionSystem(this.context.EntityLookup, this.context.Projectiles), .5f);
            this.Run(new ProjectileHitSystem(this.context.EntityLookup, this.context.EnemySpatialIndex, this.context.ObstacleGridLookup, this.context.Projectiles));
            Assert.That(this.context.DamageRequests.Items.ToArray().Any(d => d.target == this.player), Is.True);
            Assert.That(this.context.DamageRequests.Items.ToArray().Any(d => d.target == this.enemy), Is.True);
            Assert.That(this.context.Stats.ActiveCount, Is.EqualTo(2), "Both teams receive burn through the same hit effect.");
        }

        [Test] public void SwappingPreservesOldProjectileBurnAndCooldownWhileNewShotUsesHoming() {
            this.Create(false); this.Fire(); var old = this.Shots().Single();
            var oldPayload = this.world.GetStash<ProjectilePayloadComponent>().Get(old);
            for (var i = 0; i < 10; i++) { Assert.That(this.Equip(this.player, 2), Is.True); Assert.That(this.Equip(this.player, 3), Is.True); }
            this.Equip(this.player, 2); this.Fire(.25f); Assert.That(this.Shots().Length, Is.EqualTo(1));
            Assert.That(this.world.GetStash<WeaponComponent>().Get(this.player).cooldown, Is.EqualTo(.25f));
            this.Fire(.25f); var next = this.Shots().Single(s => s != old);
            Assert.That(this.world.GetStash<ProjectilePayloadComponent>().Get(old).definitionIndex, Is.EqualTo(oldPayload.definitionIndex));
            Assert.That(this.world.GetStash<ProjectilePayloadComponent>().Get(next).definitionIndex, Is.Not.EqualTo(oldPayload.definitionIndex));
            this.world.GetStash<PositionComponent>().Get(this.enemy).value = Vector3.forward * 5f;
            this.Run(new ProjectileMotionSystem(this.context.EntityLookup, this.context.Projectiles), .1f);
            Assert.That(this.world.GetStash<PositionComponent>().Get(old).value, Is.EqualTo(Vector3.right));
            Assert.That(this.world.GetStash<PositionComponent>().Get(next).value, Is.EqualTo(Vector3.forward));
            this.world.GetStash<PositionComponent>().Get(this.enemy).value = Vector3.right * 5f;
            this.Run(new EnemySpatialIndexSystem(this.context.EnemySpatialIndex));
            this.Run(new ProjectileMotionSystem(this.context.EntityLookup, this.context.Projectiles), .4f);
            this.Run(new ProjectileHitSystem(this.context.EntityLookup, this.context.EnemySpatialIndex, this.context.ObstacleGridLookup, this.context.Projectiles));
            Assert.That(this.context.Stats.ActiveCount, Is.EqualTo(1), "The already fired incendiary projectile must still ignite.");
        }

        [Test] public void FailedEquipmentChangesDoNotMutateTheSlotOrArmAnUnbudgetedActor() {
            this.Create(false); this.Fire(); var before = this.world.GetStash<WeaponComponent>().Get(this.player);
            Assert.That(this.Equip(this.player, 999), Is.False);
            Assert.That(this.Equip(this.enemy, 2), Is.False);
            Assert.That(this.Equip(this.Shots().Single(), 2), Is.False);
            using var otherWorld = World.Create("Foreign equipment"); var foreign = otherWorld.CreateEntity();
            Assert.That(this.Equip(foreign, 2), Is.False);
            this.world.GetStash<HealthComponent>().Get(this.player).current = 0f;
            Assert.That(this.Equip(this.player, 2), Is.False);
            Assert.That(this.world.GetStash<WeaponComponent>().Get(this.player), Is.EqualTo(before));
            this.world.RemoveEntity(this.player); this.world.Commit(); Assert.That(this.Equip(this.player, 2), Is.False);
        }

        [Test] public void TargetPoliciesDistinguishNearestActorAndDirectionAndRejectInvalidTargets() {
            this.Create(false); var farther = this.factory.Enemies.Spawn(Vector3.forward * 8f, this.context.Config.EnemyTypes[0]);
            this.Run(new EnemySpatialIndexSystem(this.context.EnemySpatialIndex)); var targeting = this.Targeting();
            Assert.That(targeting.TryAim(this.player, default, 10f, out var nearest), Is.True);
            Assert.That(nearest.Target, Is.EqualTo(this.enemy.ID));
            var explicitAim = new AttackAim { kind = AttackAimKind.Actor, actorId = this.world.GetStash<ActorComponent>().Get(farther).id };
            Assert.That(targeting.TryAim(this.player, explicitAim, 10f, out var selected), Is.True);
            Assert.That(selected.Target, Is.EqualTo(farther.ID));
            Assert.That(targeting.TryAim(this.player, explicitAim, 7f, out _), Is.False);
            this.world.GetStash<DamageSourceComponent>().Get(farther).team = Team.Player;
            Assert.That(targeting.TryAim(this.player, explicitAim, 10f, out _), Is.False);
            this.world.GetStash<DamageSourceComponent>().Get(farther).team = Team.Enemy;
            this.world.GetStash<HealthComponent>().Get(farther).current = 0f;
            Assert.That(targeting.TryAim(this.player, explicitAim, 10f, out _), Is.False);
            this.context.Stats.Release(farther); this.world.RemoveEntity(farther); this.world.Commit();
            this.factory.Enemies.Spawn(Vector3.forward * 8f, this.context.Config.EnemyTypes[0]); this.world.Commit();
            Assert.That(targeting.TryAim(this.player, explicitAim, 10f, out _), Is.False, "An old actor ID cannot select a replacement.");
            Assert.That(targeting.TryAim(this.player, new AttackAim { kind = AttackAimKind.Direction, direction = new Vector2(-3, 4) }, 10f, out var direction), Is.True);
            Assert.That(direction.Direction.x, Is.EqualTo(-.6f).Within(.00001f));
            Assert.That(direction.Target, Is.EqualTo(default(Scellecs.Morpeh.EntityId)));
            Assert.That(targeting.TryAim(this.player, new AttackAim { kind = AttackAimKind.Direction }, 10f, out _), Is.False);
            Assert.That(targeting.TryAim(this.player, new AttackAim { kind = (AttackAimKind)99 }, 10f, out _), Is.False);
        }

        private sealed class EquipmentChangingSpawner : IProjectileSpawner {
            private readonly IProjectileSpawner inner; private readonly Action change;
            public EquipmentChangingSpawner(IProjectileSpawner inner, Action change) { this.inner = inner; this.change = change; }
            public Entity Spawn(in ProjectileSpawn request) { this.change(); return this.inner.Spawn(request); }
        }
        [Test] public void EquipmentChangedInsideSpawnerCallbackIsNotOverwrittenByOldShotState() {
            this.Create(false);
            this.Fire(spawner: new EquipmentChangingSpawner(this.factory.Projectiles, () => this.Equip(this.player, 2)));
            var state = this.world.GetStash<WeaponComponent>().Get(this.player);
            Assert.That(this.context.Weapons.Get(state.definitionIndex).Definition.Id, Is.EqualTo(2));
            Assert.That(state.cooldown, Is.EqualTo(.5f));
            Assert.That(this.world.GetStash<ProjectilePayloadComponent>().Get(this.Shots().Single()).definitionIndex,
                Is.EqualTo(this.context.Weapons.Get(this.context.Weapons.IndexOf(3)).ProjectileIndex));
        }

        [Test] public void CatalogBudgetsMixedLifetimesAndCadencesAndSnapshotsSharedAssetsOnce() {
            this.fire.projectile.lifetime = 8f; this.homing.projectile.lifetime = 1f;
            this.config.availableWeapons = new[] { this.homing, this.fire, this.homing };
            var settings = this.config.CreateSimulationSettings();
            Assert.That(settings.Weapons.Definitions.Count, Is.EqualTo(2));
            Assert.That(settings.PlayerWeapon, Is.SameAs(settings.EnemyTypes[0].Weapon));
            Assert.That(settings.ArmedActorCapacity, Is.EqualTo(4));
            Assert.That(settings.ProjectileCapacity, Is.EqualTo(328));
            this.fire.projectile.lifetime = 1f; this.fire.id = 10;
            Assert.That(settings.PlayerWeapon.Id, Is.EqualTo(3)); Assert.That(settings.PlayerWeapon.Projectile.Lifetime, Is.EqualTo(8f));
            this.homing.id = 10; Assert.Throws<ArgumentException>(() => this.config.CreateSimulationSettings());
        }
        [Test] public void UnequippedPeriodicWeaponStillReservesItsDamageBudget() {
            this.config.enemyTypes[0].weapon = null; this.config.playerWeapon = this.homing;
            this.config.availableWeapons = Array.Empty<WeaponAsset>(); var plain = this.config.CreateSimulationSettings();
            this.config.availableWeapons = new[] { this.fire }; var fireAvailable = this.config.CreateSimulationSettings();
            Assert.That(fireAvailable.ProjectileCapacity, Is.EqualTo(plain.ProjectileCapacity));
            Assert.That(fireAvailable.DamageCapacity - plain.DamageCapacity, Is.EqualTo(fireAvailable.StatOwnerCapacity * 16));
        }

        [Test] public void RepeatedEquipmentChangesAllocateNoManagedMemoryAfterPreparation() {
            this.Create(false);
            for (var i = 0; i < 100; i++) this.Equip(this.player, i % 2 + 2);
            using var allocations = new Game.Diagnostics.AllocationMeter();
            Assert.That(allocations.Supported, Is.True);
            allocations.Start();
            for (var i = 0; i < 1000; i++) this.Equip(this.player, i % 2 + 2);
            allocations.Stop();
            Assert.That(allocations.Events, Is.Zero);
        }

        [Test] public void RepeatedSwapsAndAllArmedWaveSlotsStayWithinProjectileBudget() {
            this.config.playerMaxHealth = this.config.enemyTypes[0].maxHealth = 1000000f;
            this.Create(pooled: true);
            var actors = new[] { this.player, this.enemy,
                this.factory.Enemies.Spawn(Vector3.forward * 5f, this.context.Config.EnemyTypes[0]),
                this.factory.Enemies.Spawn(Vector3.left * 5f, this.context.Config.EnemyTypes[0]) };
            this.world.GetStash<GameStateComponent>().Get(this.context.EntityLookup.gameState).spawnedEnemies = 3;
            this.context.InputState.Frame = new PlayerInputFrame { attackHeld = true };
            new GameplayInstaller(this.context, this.factory).Install(this.world);
            this.world.Update(0f);
            for (var i = 0; i < 600; i++) {
                foreach (var actor in actors) this.Equip(actor, i / 11 % 2 + 2);
                this.world.Update(GameSession.FixedStep);
            }
            var attacks = this.world.GetStash<BattleStatisticsComponent>().Get(this.context.EntityLookup.gameState).value.attacks;
            Assert.That(attacks, Is.GreaterThan(10));
            Assert.That(this.context.ProjectilePool.PeakActiveCount, Is.LessThan(attacks), "Repeated shots must reuse returned slots.");
            Assert.That(this.context.ProjectilePool.CreatedCount, Is.EqualTo(this.context.Config.ProjectileCapacity));
        }

        [Test] public void PublicSessionAcceptsStableActorEquipmentAndDirectionAcrossFixedSteps() {
            this.config.enemyTypes[0].weapon = null;
            using var session = new GameSession(this.config.CreateSimulationSettings());
            var actors = new ActorView[session.ViewCapacity]; var count = session.CopyActors(actors);
            var id = actors.Take(count).Single(a => a.kind == ActorKind.Player).id;
            Assert.That(session.TryEquipWeapon(id, 2), Is.True);
            Assert.That(session.TryEquipWeapon(id, 999), Is.False); Assert.That(session.TryEquipWeapon(-1, 2), Is.False);
            for (var i = 0; i < 180 && session.Snapshot.phase == SessionPhase.AwaitingEntry; i++)
                session.TickUpdate(GameSession.FixedStep, new PlayerInputFrame { move = Vector2.up });
            Assert.That(session.Snapshot.phase, Is.EqualTo(SessionPhase.Combat));
            var input = new PlayerInputFrame { attackHeld = true, aim = new AttackAim { kind = AttackAimKind.Direction, direction = Vector2.right } };
            session.TickUpdate(0f, input); Assert.That(session.Snapshot.statistics.attacks, Is.Zero);
            session.TickUpdate(GameSession.FixedStep * 4, input);
            count = session.CopyActors(actors); var shot = actors.Take(count).Single(a => a.kind == ActorKind.Projectile);
            Assert.That(shot.position.x, Is.GreaterThan(session.PlayerPosition.x));
            Assert.That(shot.position.z, Is.EqualTo(session.PlayerPosition.z));
            Assert.That(session.TryEquipWeapon(shot.id, 3), Is.False);
            Assert.That(session.TryEquipWeapon(id, 3), Is.True);
            session.TickUpdate(GameSession.FixedStep, input); Assert.That(session.Snapshot.statistics.attacks, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => session.TickUpdate(0f, new PlayerInputFrame { aim = new AttackAim { direction = new Vector2(float.NaN, 0) } }));
            session.Dispose(); Assert.Throws<ObjectDisposedException>(() => session.TryEquipWeapon(id, 2));
        }
    }
}

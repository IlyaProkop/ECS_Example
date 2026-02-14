using Game.ECS.Spawning;
using System;
using Game.ECS.Abilities;
using Game.Config;
using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.ECS.Systems;
using Game.Input;
using Game.Spatial;
using NUnit.Framework;
using Scellecs.Morpeh;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class TestInput : IPlayerInputReader {
        public PlayerInputFrame frame;
        public PlayerInputFrame ReadFrame() => this.frame;
    }

    public sealed class CombatTests {
        private World world;
        private GameConfig config;
        private GameContext context;
        private EntityFactory factory;
        private TestInput input;
        private Entity player;
        private Entity state;

        [SetUp]
        public void SetUp() {
            this.config = ScriptableObject.CreateInstance<GameConfig>();
            this.config.obstaclePositions = Array.Empty<Vector3>();
            this.config.criticalChance = 0f;
            this.config.coinDropChance = 1f;
            this.config.resultsDelay = 0.2f;
            this.input = new TestInput();
            this.RebuildSimulation();
        }

        private void RebuildSimulation() {
            this.world?.Dispose();
            this.context?.Dispose();
            this.world = World.Create("Combat Tests");
            this.world.UpdateByUnity = false;
            this.context = new GameContext(this.config.CreateSimulationSettings());
            this.factory = new EntityFactory(this.world, this.context.Config, this.context.NavigationRandom, this.context.Stats, this.context.Projectiles, this.context.Weapons);
            this.state = this.factory.CreateGameStateEntity();
            this.player = this.factory.Players.Spawn(this.config.PlayerSpawn);
            this.context.EntityLookup.gameState = this.state;
            this.context.EntityLookup.player = this.player;
            this.world.Commit();
        }

        [TearDown]
        public void TearDown() {
            this.world.Dispose();
            this.context.Dispose();
            this.world = null;
            this.context = null;
            Object.DestroyImmediate(this.config);
        }

        private void Run(ISystem system, float dt = 0f) {
            system.World = this.world;
            system.OnAwake();
            this.world.Commit();
            system.OnUpdate(dt);
            this.world.Commit();
            system.Dispose();
        }

        private void RunAbility(float dt = 0f) {
            var registry = AbilityModule.CreateRegistry(this.world, this.context.EnemySpatialIndex,
                this.context.ObstacleGridLookup, this.context.DamageRequests, this.context.Stats);
            this.Run(new PlayerAbilityIntentSystem(this.context.EntityLookup));
            this.Run(new AbilityActivationSystem(this.context.EntityLookup, this.context.AbilityCasts), dt);
            this.Run(new AbilityExecutionSystem(this.context.AbilityCasts, this.context.Config.AbilityDefinitions, registry));
            this.Run(new AbilityFactsSystem(this.context.EntityLookup, this.context.AbilityCasts, this.context.AbilityEvents));
            this.context.Stats.RecalculateDirty();
        }

        private void ApplyDamage() {
            this.Run(new ApplyDamageEventsSystem(this.context.EntityLookup, this.context.DamageRequests,
                this.context.DamageApplied, this.context.DamagePipeline, this.context.CombatRandom));
            this.Run(new CombatStatisticsSystem(this.context.EntityLookup, this.context.DamageApplied));
        }

        private void EnterCombat() {
            this.world.GetStash<GameStateComponent>().Get(this.state).phase = SessionPhase.Combat;
            this.world.GetStash<PositionComponent>().Get(this.player).value = Vector3.zero;
        }

        private void Damage(Entity target, float amount, Team team = Team.Player) {
            this.context.DamageRequests.Add(new DamageRequest {
                target = target, value = amount,
                source = new DamageSourceComponent { owner = this.player.ID, team = team, criticalMultiplier = 1f }
            });
        }

        [Test]
        public void EntryStartsCombatOnlyAfterPlayerIsInside() {
            this.Run(new ArenaSessionSystem(this.context.Config, this.context.EntityLookup));
            this.Run(new EnemySpawnSystem(this.context.Config, this.context.EntityLookup, this.context.SpawnRandom, this.factory.Enemies, this.context.ObstacleGridLookup), 10f);
            Assert.That(this.world.GetStash<GameStateComponent>().Get(this.state).phase, Is.EqualTo(SessionPhase.AwaitingEntry));
            Assert.That(this.world.Filter.With<EnemyTag>().Build().GetLengthSlow(), Is.Zero);
            Assert.That(this.world.GetStash<BattleStatisticsComponent>().Get(this.state).value.duration, Is.Zero);
            this.world.GetStash<PositionComponent>().Get(this.player).value = new Vector3(0f, 0f, -9f);
            this.Run(new ArenaSessionSystem(this.context.Config, this.context.EntityLookup));
            this.Run(new EnemySpawnSystem(this.context.Config, this.context.EntityLookup, this.context.SpawnRandom, this.factory.Enemies, this.context.ObstacleGridLookup));
            Assert.That(this.world.GetStash<GameStateComponent>().Get(this.state).phase, Is.EqualTo(SessionPhase.Combat));
            Assert.That(this.world.Filter.With<EnemyTag>().Build().GetLengthSlow(), Is.EqualTo(1));
        }

        [Test]
        public void ContactUsesArmorAndRecordsActualDamage() {
            this.EnterCombat();
            var enemy = this.factory.Enemies.Spawn(Vector3.right, this.context.Config.EnemyTypes[0]);
            this.Run(new EnemySpatialIndexSystem(this.context.EnemySpatialIndex));
            this.Run(new EnemyContactDamageSystem(this.context.EntityLookup, this.context.EnemySpatialIndex, this.context.DamageRequests), 1f);
            Assert.That(this.world.GetStash<HealthComponent>().Get(this.player).current, Is.EqualTo(100f));
            this.ApplyDamage();
            var actual = 5f * 100f / 110f;
            Assert.That(this.world.GetStash<HealthComponent>().Get(this.player).current, Is.EqualTo(100f - actual).Within(0.0001f));
            Assert.That(this.world.GetStash<BattleStatisticsComponent>().Get(this.state).value.damageReceived, Is.EqualTo(actual).Within(0.0001f));
            Assert.That(this.context.DamageRequests.Count, Is.Zero);
        }

        [Test]
        public void StatusArmorReachesTheCommonDamagePipelineAndExpiresToBaseArmor() {
            this.EnterCombat();
            this.world.GetStash<CombatInputComponent>().Get(this.player).abilityPressed = true;
            this.RunAbility();
            this.Damage(this.player, 14f, Team.Enemy);
            this.ApplyDamage();
            Assert.That(this.world.GetStash<HealthComponent>().Get(this.player).current, Is.EqualTo(90f).Within(0.0001f));
            this.context.Stats.Advance(3f); this.context.Stats.RecalculateDirty();
            this.Damage(this.player, 11f, Team.Enemy);
            this.ApplyDamage();
            Assert.That(this.world.GetStash<HealthComponent>().Get(this.player).current, Is.EqualTo(80f).Within(0.0001f));
            Assert.That(this.world.GetStash<BattleStatisticsComponent>().Get(this.state).value.damageReceived, Is.EqualTo(20f).Within(0.0001f));
        }

        [Test]
        public void MultipleLethalHitsCountHealthAndDeathOnlyOnce() {
            this.EnterCombat();
            var enemy = this.factory.Enemies.Spawn(Vector3.right * 5f, this.context.Config.EnemyTypes[0]);
            this.Damage(enemy, 100f);
            this.Damage(enemy, 100f);
            this.ApplyDamage();
            this.Run(new EnemyDeathSystem(this.context.Config, this.context.EntityLookup, this.context.LootRandom, this.factory.Coins));
            this.Run(new EnemyDeathSystem(this.context.Config, this.context.EntityLookup, this.context.LootRandom, this.factory.Coins));
            var stats = this.world.GetStash<BattleStatisticsComponent>().Get(this.state).value;
            Assert.That(stats.damageDealt, Is.EqualTo(this.config.enemyTypes[0].maxHealth));
            Assert.That(stats.kills, Is.EqualTo(1));
            Assert.That(this.world.Filter.With<CoinTag>().Build().GetLengthSlow(), Is.EqualTo(1));
            this.Run(new DestroyMarkedEntitiesSystem());
            Assert.That(enemy.IsNullOrDisposed(), Is.True);
        }

        [Test]
        public void DamageToDestroyedTargetIsConsumedWithoutTouchingNewEntities() {
            this.EnterCombat();
            var enemy = this.factory.Enemies.Spawn(Vector3.right * 5f, this.context.Config.EnemyTypes[0]);
            this.Damage(enemy, 100f);
            this.world.RemoveEntity(enemy);
            this.world.Commit();
            var replacement = this.factory.Enemies.Spawn(Vector3.left * 5f, this.context.Config.EnemyTypes[0]);
            this.ApplyDamage();
            Assert.That(this.world.GetStash<HealthComponent>().Get(replacement).current, Is.EqualTo(this.config.enemyTypes[0].maxHealth));
            Assert.That(this.context.DamageRequests.Count, Is.Zero);
        }

        [Test]
        public void VictoryWaitsForAllSpawnsAndDefeatWinsATie() {
            this.EnterCombat();
            this.Run(new ApplyGameOverSystem(this.context.Config, this.context.EntityLookup));
            Assert.That(this.world.GetStash<GameStateComponent>().Get(this.state).phase, Is.EqualTo(SessionPhase.Combat));
            this.world.GetStash<GameStateComponent>().Get(this.state).spawnedEnemies = this.config.enemyCount;
            this.world.GetStash<HealthComponent>().Get(this.player).current = 0f;
            this.Run(new ApplyGameOverSystem(this.context.Config, this.context.EntityLookup));
            Assert.That(this.world.GetStash<GameStateComponent>().Get(this.state).outcome, Is.EqualTo(BattleOutcome.Defeat));
        }

        [Test]
        public void ResultsFreezeCombatAndThenEnterMeta() {
            this.EnterCombat();
            this.world.GetStash<GameStateComponent>().Get(this.state).spawnedEnemies = this.config.enemyCount;
            this.Run(new ApplyGameOverSystem(this.context.Config, this.context.EntityLookup));
            Assert.That(this.world.GetStash<GameStateComponent>().Get(this.state).outcome, Is.EqualTo(BattleOutcome.Victory));
            this.Damage(this.player, 10000f, Team.Enemy);
            this.ApplyDamage();
            this.Run(new ArenaSessionSystem(this.context.Config, this.context.EntityLookup), 0.25f);
            Assert.That(this.world.GetStash<GameStateComponent>().Get(this.state).phase, Is.EqualTo(SessionPhase.Meta));
            Assert.That(this.world.GetStash<HealthComponent>().Get(this.player).current, Is.EqualTo(100f));
            Assert.That(this.world.GetStash<BattleStatisticsComponent>().Get(this.state).value.duration, Is.Zero);
        }

        [Test]
        public void AbilityRespectsRangeAndCooldown() {
            this.EnterCombat();
            var near = this.factory.Enemies.Spawn(Vector3.right * 2f, this.context.Config.EnemyTypes[0]);
            var far = this.factory.Enemies.Spawn(Vector3.left * 8f, this.context.Config.EnemyTypes[0]);
            this.Run(new EnemySpatialIndexSystem(this.context.EnemySpatialIndex));
            this.world.GetStash<CombatInputComponent>().Get(this.player).abilityPressed = true;
            this.RunAbility();
            this.ApplyDamage();
            this.RunAbility(0.1f);
            Assert.That(this.world.GetStash<HealthComponent>().Get(near).current, Is.Zero);
            Assert.That(this.world.GetStash<HealthComponent>().Get(far).current, Is.EqualTo(this.config.enemyTypes[0].maxHealth));
            Assert.That(this.world.GetStash<BattleStatisticsComponent>().Get(this.state).value.abilityUses, Is.EqualTo(1));
            Assert.That(this.world.GetStash<AbilityComponent>().Get(this.player).remainingCooldown, Is.EqualTo(4.9f).Within(0.001f));
        }

        [Test]
        public void BasicAttackRequiresInputAndRespectsCooldown() {
            this.EnterCombat();
            this.factory.Enemies.Spawn(Vector3.right * 5f, this.context.Config.EnemyTypes[0]);
            this.Run(new EnemySpatialIndexSystem(this.context.EnemySpatialIndex));
            SharedWeaponTestRunner.Fire(this.world, this.context, this.factory.Projectiles, 1f);
            Assert.That(this.world.Filter.With<ProjectileTag>().Build().GetLengthSlow(), Is.Zero);
            this.world.GetStash<CombatInputComponent>().Get(this.player).attackHeld = true;
            SharedWeaponTestRunner.Fire(this.world, this.context, this.factory.Projectiles);
            SharedWeaponTestRunner.Fire(this.world, this.context, this.factory.Projectiles, 0.1f);
            Assert.That(this.world.Filter.With<ProjectileTag>().Build().GetLengthSlow(), Is.EqualTo(1));
            Assert.That(this.world.GetStash<BattleStatisticsComponent>().Get(this.state).value.attacks, Is.EqualTo(1));
        }

        [Test]
        public void ObstaclesBlockBothProjectileAndAreaDamage() {
            this.config.obstaclePositions = new[] { new Vector3(1.5f, 0f, 0f) };
            this.config.obstacleScale = Vector3.one;
            this.RebuildSimulation();
            this.EnterCombat();
            var enemy = this.factory.Enemies.Spawn(Vector3.right * 3f, this.context.Config.EnemyTypes[0]);
            this.Run(new EnemySpatialIndexSystem(this.context.EnemySpatialIndex));
            this.world.GetStash<CombatInputComponent>().Get(this.player).abilityPressed = true;
            this.RunAbility();
            var projectile = this.factory.Projectiles.Spawn(new ProjectileSpawn(Vector3.zero, Vector3.right, 100f, 25f, 0.2f, 1f,
                this.world.GetStash<DamageSourceComponent>().Get(this.player)));
            this.Run(new Game.ECS.Projectiles.ProjectileMotionSystem(this.context.EntityLookup, this.context.Projectiles), 0.1f);
            this.Run(new ProjectileHitSystem(this.context.EntityLookup, this.context.EnemySpatialIndex, this.context.ObstacleGridLookup, this.context.Projectiles), 0.1f);
            this.ApplyDamage();
            Assert.That(this.world.GetStash<HealthComponent>().Get(enemy).current, Is.EqualTo(60f));
            Assert.That(this.world.GetStash<DestroyTag>().Has(projectile), Is.True);
            Assert.That(this.world.GetStash<BattleStatisticsComponent>().Get(this.state).value.damageDealt, Is.Zero);
        }

        [Test]
        public void FailedSpawnDoesNotConsumeWaveBudgetAndSuccessfulSpawnsAreBounded() {
            this.EnterCombat();
            this.config.obstaclePositions = new[] { Vector3.zero };
            this.config.obstacleScale = new Vector3(100f, 1f, 100f);
            this.RebuildSimulation();
            this.EnterCombat();
            this.Run(new EnemySpawnSystem(this.context.Config, this.context.EntityLookup, this.context.SpawnRandom, this.factory.Enemies, this.context.ObstacleGridLookup), 1f);
            Assert.That(this.world.GetStash<GameStateComponent>().Get(this.state).spawnedEnemies, Is.Zero);
            this.config.obstaclePositions = Array.Empty<Vector3>();
            this.RebuildSimulation();
            this.EnterCombat();
            for (var i = 0; i < this.config.enemyCount + 5; i++) this.Run(new EnemySpawnSystem(this.context.Config, this.context.EntityLookup, this.context.SpawnRandom, this.factory.Enemies, this.context.ObstacleGridLookup), 1f);
            Assert.That(this.world.GetStash<GameStateComponent>().Get(this.state).spawnedEnemies, Is.EqualTo(this.config.enemyCount));
            Assert.That(this.world.Filter.With<EnemyTag>().Build().GetLengthSlow(), Is.EqualTo(this.config.enemyCount));
        }

        [Test]
        public void FastProjectileHitsFirstEnemyAndDoesNotOutliveItsRange() {
            this.EnterCombat();
            var near = this.factory.Enemies.Spawn(Vector3.right * 3f, this.context.Config.EnemyTypes[0]);
            var far = this.factory.Enemies.Spawn(Vector3.right * 6f, this.context.Config.EnemyTypes[0]);
            this.Run(new EnemySpatialIndexSystem(this.context.EnemySpatialIndex));
            this.factory.Projectiles.Spawn(new ProjectileSpawn(Vector3.zero, Vector3.right, 100f, 25f, 0.2f, 1f,
                this.world.GetStash<DamageSourceComponent>().Get(this.player)));
            this.Run(new Game.ECS.Projectiles.ProjectileMotionSystem(this.context.EntityLookup, this.context.Projectiles), 0.1f);
            this.Run(new ProjectileHitSystem(this.context.EntityLookup, this.context.EnemySpatialIndex, this.context.ObstacleGridLookup, this.context.Projectiles), 0.1f);
            this.ApplyDamage();
            Assert.That(this.world.GetStash<HealthComponent>().Get(near).current, Is.EqualTo(35f));
            Assert.That(this.world.GetStash<HealthComponent>().Get(far).current, Is.EqualTo(60f));
            this.factory.Projectiles.Spawn(new ProjectileSpawn(Vector3.zero, Vector3.right, 100f, 25f, 0.2f, 0.01f,
                this.world.GetStash<DamageSourceComponent>().Get(this.player)));
            this.Run(new Game.ECS.Projectiles.ProjectileMotionSystem(this.context.EntityLookup, this.context.Projectiles), 0.1f);
            this.Run(new ProjectileHitSystem(this.context.EntityLookup, this.context.EnemySpatialIndex, this.context.ObstacleGridLookup, this.context.Projectiles), 0.1f);
            this.ApplyDamage();
            Assert.That(this.world.GetStash<HealthComponent>().Get(near).current, Is.EqualTo(35f));
        }

        [Test]
        public void ClosedArenaAndSweptMovementPreventEscapesAndTunnelling() {
            this.config.obstaclePositions = new[] { Vector3.zero };
            var settings = this.config.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(settings, 0.5f);
            var motion = new ActorMotionQuery(settings.arenaHalfSize, 0.5f, true, obstacles.AsNative());
            var position = motion.Move(new Vector3(-5f, 0f, 0f), Vector3.right * 20f);
            Assert.That(position.x, Is.LessThanOrEqualTo(-2f));
            var gate = motion.Move(new Vector3(0f, 0f, -9f), Vector3.back * 20f);
            Assert.That(gate.z, Is.EqualTo(-9.5f).Within(0.001f));
        }

        [Test]
        public void FullPipelineCompletesFiniteBattleAndCanRestart() {
            this.config.enemyCount = 3;
            this.config.enemyTypes[0].maxHealth = 25f;
            this.config.enemyTypes[0].moveSpeed = 1f;
            for (var run = 0; run < 3; run++) {
                using var session = new GameSession(this.config.CreateSimulationSettings());
                Assert.That(session.Snapshot.phase, Is.EqualTo(SessionPhase.AwaitingEntry));
                Assert.That(session.Snapshot.statistics.kills, Is.Zero);
                for (var frame = 0; frame < 1200 && !session.TryGetResult(out _); frame++) {
                    this.input.frame = new PlayerInputFrame {
                        move = frame < 32 ? Vector2.up : Vector2.zero,
                        attackHeld = true, abilityPressed = frame % 150 == 0
                    };
                    session.TickUpdate(1f / 30f, this.input.frame);
                }
                Assert.That(session.TryGetResult(out var result), Is.True);
                Assert.That(result.outcome, Is.EqualTo(BattleOutcome.Victory));
                Assert.That(result.statistics.kills, Is.EqualTo(3));
                Assert.That(result.reward, Is.GreaterThanOrEqualTo(this.config.victoryReward));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Config;
using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.ECS.Navigation;
using Game.ECS.Systems;
using Game.Input;
using Game.Progression;
using NUnit.Framework;
using Scellecs.Morpeh;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class ArchitectureContractTests {
        private GameConfig config;
        [SetUp] public void SetUp() {
            this.config = ScriptableObject.CreateInstance<GameConfig>();
            this.config.obstaclePositions = Array.Empty<Vector3>();
            this.config.criticalChance = 0f;
            this.config.enemyTypes[0].damagePerSecond = 0f;
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(this.config);

        [Test]
        public void SimulationHasNoReferenceToAuthoringPresentationOrInputSystem() {
            var assembly = typeof(GameSession).Assembly;
            var names = assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(names, Does.Not.Contain("Game.Authoring"));
            Assert.That(names, Does.Not.Contain("Game.Presentation"));
            Assert.That(names, Does.Not.Contain("Unity.InputSystem"));
            Assert.That(names, Does.Not.Contain("UnityEngine.UI"));
            Assert.That(assembly.GetExportedTypes(), Is.EquivalentTo(new[] { typeof(GameSession) }));
        }

        [Test]
        public void AllSimulationComponentsContainOnlyValueData() {
            foreach (var type in typeof(GameSession).Assembly.GetTypes().Where(t => typeof(IComponent).IsAssignableFrom(t))) {
                Assert.That(ContainsReference(type), Is.False, type.FullName);
            }
        }

        [Test]
        public void GameplaySystemsCannotReachSessionOrFactoryBundles() {
            foreach (var type in typeof(GameSession).Assembly.GetTypes().Where(t => typeof(ISystem).IsAssignableFrom(t))) {
                Assert.That(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Any(field => field.FieldType == typeof(GameContext) || field.FieldType == typeof(EntityFactory)), Is.False, type.FullName);
                Assert.That(type.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                    .Any(parameter => parameter.ParameterType == typeof(GameContext) || parameter.ParameterType == typeof(EntityFactory)), Is.False, type.FullName);
            }
        }

        [Test]
        public void ApplicationPolicyDependsOnContractsWithoutUnityOrEcs() {
            var references = typeof(Game.Flow.GameFlowController).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(references.Any(name => name.StartsWith("Unity") || name == "Game.ECS" || name == "Game.Presentation" || name == "Game.Authoring"), Is.False);
            Assert.That(typeof(ProgressService).Assembly, Is.EqualTo(typeof(Game.Flow.GameFlowController).Assembly));
        }
        private static bool ContainsReference(Type type) {
            if (!type.IsValueType) return true;
            if (type.IsPrimitive || type.IsEnum) return false;
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Any(f => ContainsReference(f.FieldType));
        }

        [Test]
        public void AuthoringAndObstacleArrayChangesCannotMutateRunningDefinition() {
            this.config.obstaclePositions = new[] { Vector3.right * 6f };
            var definition = this.config.CreateSimulationSettings();
            this.config.enemyTypes[0].maxHealth = 999f;
            this.config.obstaclePositions[0] = Vector3.zero;
            Assert.That(definition.EnemyTypes[0].MaxHealth, Is.EqualTo(60f));
            Assert.That(definition.obstaclePositions[0], Is.EqualTo(Vector3.right * 6f));
            Assert.Throws<NotSupportedException>(() => ((IList<Vector3>)definition.obstaclePositions)[0] = Vector3.left);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void InvalidDefinitionFailsBeforeWorldCreation(float speed) {
            this.config.playerMoveSpeed = speed;
            Assert.Throws<ArgumentOutOfRangeException>(() => this.config.CreateSimulationSettings());
        }

        [Test]
        public void BufferOverflowCannotOverwriteOrSilentlyDiscardAnEarlierHit() {
            var buffer = new FrameBuffer<DamageRequest>(1);
            buffer.Add(new DamageRequest { value = 25f });
            Assert.Throws<InvalidOperationException>(() => buffer.Add(new DamageRequest { value = 50f }));
            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.Items[0].value, Is.EqualTo(25f));
            buffer.Clear();
            Assert.That(buffer.Count, Is.Zero);
            buffer.Add(new DamageRequest { value = 10f });
            Assert.That(buffer.PeakCount, Is.EqualTo(1));
        }

        [Test]
        public void PathSlotsDoNotShareMemoryAndLongPathsHaveABoundedPrefix() {
            var paths = new PathStorage(2, 4);
            var points = new List<Vector3> { Vector3.zero, Vector3.right, Vector3.right * 2, Vector3.right * 3, Vector3.right * 4 };
            Assert.That(paths.Write(0, points, 0), Is.EqualTo(4));
            paths.Write(1, new List<Vector3> { Vector3.left }, 0);
            Assert.That(paths.Get(0, 3), Is.EqualTo(Vector3.right * 3));
            Assert.That(paths.Get(1, 0), Is.EqualTo(Vector3.left));
            Assert.Throws<ArgumentOutOfRangeException>(() => paths.Get(2, 0));
        }

        [Test]
        public void FixedTicksGiveTheSameStateForDifferentHostFrameRates() {
            using var a = new GameSession(this.config.CreateSimulationSettings());
            using var b = new GameSession(this.config.CreateSimulationSettings());
            var input = new PlayerInputFrame { move = Vector2.up };
            for (var i = 0; i < 120; i++) a.TickUpdate(GameSession.FixedStep, input);
            for (var i = 0; i < 60; i++) b.TickUpdate(GameSession.FixedStep * 2, input);
            Assert.That(a.PlayerPosition, Is.EqualTo(b.PlayerPosition));
            Assert.That(a.Snapshot.statistics.duration, Is.EqualTo(b.Snapshot.statistics.duration));
            Assert.That(a.DroppedSimulationSeconds, Is.Zero);
        }

        [Test]
        public void PressEdgeSurvivesNoStepAndIsConsumedOnlyOnceInCatchUp() {
            using var session = new GameSession(this.config.CreateSimulationSettings(Game.Domain.Abilities.BuiltInAbilities.CreateShockwave(cooldown: 0.001f)));
            Enter(session);
            session.TickUpdate(0f, new PlayerInputFrame { abilityPressed = true });
            Assert.That(session.Snapshot.statistics.abilityUses, Is.Zero);
            session.TickUpdate(GameSession.FixedStep * 4, default);
            Assert.That(session.Snapshot.statistics.abilityUses, Is.EqualTo(1));
        }

        [Test]
        public void CatchUpIsBoundedAndDroppedTimeIsObservable() {
            using var session = new GameSession(this.config.CreateSimulationSettings());
            session.TickUpdate(2f, new PlayerInputFrame { move = Vector2.up });
            Assert.That(session.DroppedSimulationSeconds, Is.GreaterThan(1.8));
            Assert.That(session.PlayerPosition.z - this.config.PlayerSpawn.z, Is.LessThan(1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.TickUpdate(float.NaN, default));
        }

        [Test]
        public void LatestContinuousInputReplacesEarlierFrameWithoutLosingPressEdge() {
            var input = new SimulationInput();
            input.Submit(new PlayerInputFrame { move = Vector2.up, attackHeld = true, abilityPressed = true });
            input.Submit(new PlayerInputFrame { move = Vector2.right * 3f });
            Assert.That(input.ReadFrame().move, Is.EqualTo(Vector2.right));
            Assert.That(input.ReadFrame().attackHeld, Is.False);
            Assert.That(input.ReadFrame().abilityPressed, Is.True);
            input.CompleteStep();
            Assert.That(input.ReadFrame().abilityPressed, Is.False);
            Assert.That(input.ReadFrame().move, Is.EqualTo(Vector2.right));
        }

        [Test]
        public void InvalidHostFrameDoesNotConsumePendingInputOrAdvanceTime() {
            using var session = new GameSession(this.config.CreateSimulationSettings());
            using var reference = new GameSession(this.config.CreateSimulationSettings());
            var pending = new PlayerInputFrame { move = Vector2.up, abilityPressed = true };
            session.TickUpdate(0f, pending);
            reference.TickUpdate(0f, pending);
            Assert.Throws<ArgumentException>(() => session.TickUpdate(2f,
                new PlayerInputFrame { move = new Vector2(float.NaN, 0f) }));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.TickUpdate(-1f, default));
            session.TickUpdate(GameSession.FixedStep, pending);
            reference.TickUpdate(GameSession.FixedStep, pending);
            Assert.That(session.PlayerPosition, Is.EqualTo(reference.PlayerPosition));
            Assert.That(session.DroppedSimulationSeconds, Is.Zero);
        }

        [Test]
        public void ClockDropsOnlyWholeStepsAndRetainsFractionForNextFrame() {
            var clock = new FixedStepClock(0.125f, 2);
            clock.BeginFrame(0.5625f);
            var steps = 0;
            while (clock.HasStep) { clock.CompleteStep(); steps++; }
            clock.EndFrame();
            Assert.That(steps, Is.EqualTo(2));
            Assert.That(clock.DroppedSeconds, Is.EqualTo(0.25d));
            Assert.That(clock.InterpolationAlpha, Is.EqualTo(0.5f));
            clock.BeginFrame(0.0625f);
            Assert.That(clock.HasStep, Is.True);
            clock.CompleteStep();
            clock.EndFrame();
            Assert.That(clock.HasStep, Is.False);
            Assert.That(clock.InterpolationAlpha, Is.Zero);
            Assert.That(clock.DroppedSeconds, Is.EqualTo(0.25d));
        }

        [Test]
        public void ViewCopiesDoNotMutateWorldAndContainUniqueLifetimeIds() {
            using var session = new GameSession(this.config.CreateSimulationSettings());
            Enter(session);
            var views = new ActorView[session.ViewCapacity];
            var count = session.CopyActors(views);
            Assert.That(count, Is.GreaterThan(1));
            Assert.That(views.Take(count).Select(v => v.id).Distinct().Count(), Is.EqualTo(count));
            var original = session.PlayerPosition;
            for (var i = 0; i < count; i++) views[i] = new ActorView(0, ActorKind.Player, Vector3.one * 999f, 0f, 0f);
            Assert.That(session.PlayerPosition, Is.EqualTo(original));
            Assert.Throws<ArgumentException>(() => session.CopyActors(new ActorView[1]));
            session.Dispose();
            Assert.Throws<ObjectDisposedException>(() => session.TickUpdate(0f, default));
            Assert.Throws<ObjectDisposedException>(() => session.CopyActors(views));
        }

        [Test]
        public void AddingADamageModifierRequiresNoChangesToAttackOrHealthSystems() {
            using var world = World.Create("Modifier Contract");
            world.UpdateByUnity = false;
            using var context = new GameContext(this.config.CreateSimulationSettings(), new DamagePipeline(new HalfDamage()));
            var factory = new EntityFactory(world, context.Config, context.NavigationRandom, context.Stats, context.Projectiles, context.Weapons);
            context.EntityLookup.gameState = factory.CreateGameStateEntity();
            context.EntityLookup.player = factory.Players.Spawn(Vector3.zero);
            var enemy = factory.Enemies.Spawn(Vector3.right, context.Config.EnemyTypes[0]);
            world.GetStash<GameStateComponent>().Get(context.EntityLookup.gameState).phase = SessionPhase.Combat;
            context.DamageRequests.Add(new DamageRequest { target = enemy, value = 40f, source = new DamageSourceComponent { team = Team.Player } });
            var system = new ApplyDamageEventsSystem(context.EntityLookup, context.DamageRequests, context.DamageApplied, context.DamagePipeline, context.CombatRandom) { World = world };
            system.OnAwake();
            world.Commit();
            system.OnUpdate(0f);
            Assert.That(world.GetStash<HealthComponent>().Get(enemy).current, Is.EqualTo(40f));
            Assert.That(world.GetStash<BattleStatisticsComponent>().Get(context.EntityLookup.gameState).value.damageDealt, Is.Zero);
            Assert.That(context.DamageApplied.Items[0].amount, Is.EqualTo(20f));
            Assert.That(context.DamageRequests.Count, Is.Zero);
        }

        [Test]
        public void SaveFailureCanRetryWithoutDoubleRewardOrExternalMutation() {
            var store = new FailingStore();
            var service = new ProgressService(store);
            var result = new BattleResult("battle-a", BattleOutcome.Victory, new BattleStatistics { kills = 12, duration = 20f }, 100);
            service.Record(result);
            Assert.That(service.HasPendingSave, Is.True);
            service.Current.currency = 999; // This is a detached copy.
            store.fail = false;
            service.Record(result);
            service.Record(new BattleResult("battle-b", BattleOutcome.Defeat, default, 0));
            service.Record(result); // Nonadjacent duplicate within this application lifetime.
            Assert.That(service.Current.currency, Is.EqualTo(100));
            Assert.That(service.Current.battles, Is.EqualTo(2));
            Assert.That(service.HasPendingSave, Is.False);
            Assert.That(store.saved.currency, Is.EqualTo(100));
        }

        private static void Enter(GameSession session) {
            for (var i = 0; i < 80; i++) session.TickUpdate(GameSession.FixedStep, new PlayerInputFrame { move = Vector2.up });
            Assert.That(session.Snapshot.phase, Is.EqualTo(SessionPhase.Combat));
        }

        [Test]
        public void LoadedRewardCheckpointRemainsProtectedAfterAnotherBattle() {
            var store = new FailingStore {
                fail = false,
                initial = new PlayerProgress { lastSessionId = "saved-a", currency = 100, battles = 1, victories = 1 }
            };
            var service = new ProgressService(store);
            var replay = new BattleResult("saved-a", BattleOutcome.Victory, default, 100);
            service.Record(replay);
            service.Record(new BattleResult("new-b", BattleOutcome.Defeat, default, 0));
            service.Record(replay);
            Assert.That(service.Current.currency, Is.EqualTo(100));
            Assert.That(service.Current.battles, Is.EqualTo(2));
        }

        [Test]
        public void NavigationBudgetDoesNotStarveEnemiesAtTheEndOfTheFilter() {
            this.config.enemyCount = 12;
            this.config.enemyPathMaxRepathsPerFrame = 1;
            this.config.enemyPathRepathInterval = 100f;
            this.config.obstaclePositions = new[] { Vector3.zero };
            using var context = new GameContext(this.config.CreateSimulationSettings());
            using var world = World.Create("Navigation Fairness");
            world.UpdateByUnity = false;
            var factory = new EntityFactory(world, context.Config, context.NavigationRandom, context.Stats, context.Projectiles, context.Weapons);
            context.EntityLookup.gameState = factory.CreateGameStateEntity();
            context.EntityLookup.player = factory.Players.Spawn(Vector3.right * 4f);
            world.GetStash<GameStateComponent>().Get(context.EntityLookup.gameState).phase = SessionPhase.Combat;
            var enemies = new Entity[12];
            for (var i = 0; i < enemies.Length; i++) enemies[i] = factory.Enemies.Spawn(Vector3.left * 4f, context.Config.EnemyTypes[0]);
            var system = new EnemyPathPlanningSystem(context.Config.Navigation, context.Config.Arena, context.Config.enemyCount, context.EntityLookup, context.ObstacleGridLookup, context.Paths, context.NavigationRandom) { World = world };
            system.OnAwake();
            world.Commit();
            var chase = new ChasePlayerSystem(context.EntityLookup) { World = world };
            chase.OnAwake(); chase.OnUpdate(GameSession.FixedStep);
            var paths = world.GetStash<EnemyPathComponent>();
            var previous = 0;
            try {
                for (var tick = 0; tick < 12; tick++) {
                    system.OnUpdate(GameSession.FixedStep);
                    var ready = enemies.Count(e => paths.Get(e).status == PathStatus.Route);
                    Assert.That(ready - previous, Is.EqualTo(1), "One route per tick, without starvation.");
                    previous = ready;
                }
            } finally { system.Dispose(); }
            Assert.That(previous, Is.EqualTo(enemies.Length));
        }
        private sealed class HalfDamage : IDamageModifier {
            public void Apply(ref DamageCalculation damage) => damage.amount *= 0.5f;
        }
        private sealed class FailingStore : IProgressStore {
            public bool fail = true;
            public PlayerProgress saved;
            public PlayerProgress initial;
            public PlayerProgress Load() => this.initial ?? new PlayerProgress();
            public void Save(PlayerProgress progress) {
                if (this.fail) throw new IOException("test storage unavailable");
                this.saved = progress.Copy();
            }
        }
    }
}

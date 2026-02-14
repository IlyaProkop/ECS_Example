using System;
using Game.Config;
using Game.Domain;
using Game.Domain.Stats;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.ECS.Stats;
using Game.Input;
using NUnit.Framework;
using Scellecs.Morpeh;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class StatMathTests {
        [Test]
        public void AdditionsPrecedeMultipliersRegardlessOfApplicationOrder() {
            var add = new StatModifierDefinition(1, StatOperation.Add, -2f);
            var multiply = new StatModifierDefinition(1, StatOperation.Multiply, 1.5f);
            var first = new StatCalculation(10f);
            first.Apply(add, 1, 0); first.Apply(multiply, 2, 0);
            var second = new StatCalculation(10f);
            second.Apply(multiply, 1, 0); second.Apply(add, 2, 0);
            var definition = StatCatalog.Default.Definitions[0];
            Assert.That(first.Resolve(definition), Is.EqualTo(12f));
            Assert.That(second.Resolve(definition), Is.EqualTo(12f));
        }

        [Test]
        public void OverrideUsesPriorityThenLatestApplicationThenModifierOrder() {
            var value = new StatCalculation(10f);
            value.Apply(new StatModifierDefinition(1, StatOperation.Override, 20f, 5), 2, 0);
            value.Apply(new StatModifierDefinition(1, StatOperation.Override, 30f, 4), 9, 0);
            value.Apply(new StatModifierDefinition(1, StatOperation.Override, 40f, 5), 3, 0);
            value.Apply(new StatModifierDefinition(1, StatOperation.Override, 50f, 5), 3, 1);
            value.Apply(new StatModifierDefinition(1, StatOperation.Multiply, 0f), 99, 0);
            Assert.That(value.Resolve(StatCatalog.Default.Definitions[0]), Is.EqualTo(50f));
        }

        [Test]
        public void ExtremeFactorsClampAndZeroNeverProducesNaN() {
            var value = new StatCalculation(10f);
            for (var i = 0; i < 16; i++) value.Apply(new StatModifierDefinition(1, StatOperation.Multiply, float.MaxValue), i, 0);
            Assert.That(value.Resolve(StatCatalog.Default.Definitions[0]), Is.EqualTo(100f));
            value.Apply(new StatModifierDefinition(1, StatOperation.Multiply, 0f), 17, 0);
            Assert.That(value.Resolve(StatCatalog.Default.Definitions[0]), Is.Zero);
            value.Apply(new StatModifierDefinition(1, StatOperation.Override, -200f), 18, 0);
            Assert.That(value.Resolve(StatCatalog.Default.Definitions[0]), Is.Zero);
        }

        [Test]
        public void InvalidContentFailsAtTheDefinitionBoundary() {
            Assert.Throws<ArgumentException>(() => new StatCatalog(StatCatalog.Default.Definitions[0], StatCatalog.Default.Definitions[0]));
            Assert.Throws<ArgumentException>(() => new StatDefinition(1, 11f, 0f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatDefinition(1, 0f, float.NaN, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatModifierDefinition(1, StatOperation.Multiply, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatModifierDefinition(1, StatOperation.Add, float.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => new StatusEffectDefinition(1, 3f, StatusStacking.Refresh, 2, new StatModifierDefinition(1, StatOperation.Add, 1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StatusEffectDefinition(1, 3f, StatusStacking.Refresh, 1, default(StatModifierDefinition)));
        }

        [Test]
        public void AuthoringAndInputArraysCannotChangeCompiledContent() {
            var authoring = ScriptableObject.CreateInstance<GameConfig>();
            try {
                var settings = authoring.CreateSimulationSettings();
                authoring.statDefinitions[0].max = 3f;
                Assert.That(settings.StatCatalog.Definitions[0].Max, Is.EqualTo(100f));
                var effect = new SelfStatusAuthoring();
                var definition = ((SelfStatusDefinition)effect.CreateDefinition()).Status;
                effect.modifiers[0].value = 99f;
                Assert.That(definition.Modifiers[0].Value, Is.EqualTo(1.35f));
                var modifiers = new[] { new StatModifierDefinition(1, StatOperation.Add, 2f) };
                var status = new StatusEffectDefinition(20, 3f, StatusStacking.Refresh, 1, modifiers);
                modifiers[0] = new StatModifierDefinition(1, StatOperation.Add, 999f);
                Assert.That(status.Modifiers[0].Value, Is.EqualTo(2f));
            } finally { Object.DestroyImmediate(authoring); }
        }
    }

    public sealed class StatStorageTests {
        private World world;
        private StatStorage stats;
        private Entity player;
        private EffectSource source;
        [SetUp] public void SetUp() {
            this.world = World.Create("Stat lifetime tests");
            this.world.UpdateByUnity = false;
            this.stats = new StatStorage(new StatCatalog(StatCatalog.Default.Definitions[0], StatCatalog.Default.Definitions[1],
                new StatDefinition(99, 5f, -100f, 100f)), 4);
            this.stats.Attach(this.world);
            this.player = this.Actor();
            this.source = new EffectSource(this.player.ID, 1);
        }
        [TearDown] public void TearDown() { this.world.Dispose(); this.stats.Dispose(); }
        private Entity Actor() {
            var entity = this.world.CreateEntity();
            this.world.GetStash<HealthComponent>().Set(entity, new HealthComponent { current = 100f, max = 100f });
            this.stats.Register(entity, 6f, 10f);
            this.world.Commit();
            return entity;
        }
        private CompiledStatus Status(int id, float duration, params StatModifierDefinition[] modifiers) =>
            this.stats.Compile(new StatusEffectDefinition(id, duration, StatusStacking.Refresh, 1, modifiers));
        private void Advance(float delta) { this.stats.Advance(delta); this.stats.RecalculateDirty(); }
        private float Speed => this.stats.GetFinal(this.player, StatIds.MoveSpeed);

        [Test]
        public void DifferentSourcesCombineAndRemovalRebuildsFromCurrentBase() {
            var buff = this.stats.Compile(BuiltInStatuses.CreateEmpowerment());
            this.stats.Apply(this.player, this.source, buff);
            var otherSource = new EffectSource(this.player.ID, 2);
            this.stats.Apply(this.player, otherSource, buff);
            this.stats.SetBase(this.player, StatIds.MoveSpeed, 8f);
            this.stats.RecalculateDirty();
            Assert.That(this.Speed, Is.EqualTo(8f * 1.35f * 1.35f).Within(0.00001f));
            Assert.That(this.stats.GetFinal(this.player, StatIds.Armor), Is.EqualTo(70f));
            Assert.That(this.stats.RemoveBySource(this.player, this.source), Is.EqualTo(1));
            this.stats.RecalculateDirty();
            Assert.That(this.Speed, Is.EqualTo(10.8f).Within(0.00001f));
            this.Advance(3f);
            Assert.That(this.Speed, Is.EqualTo(8f));
            Assert.That(this.stats.GetFinal(this.player, StatIds.Armor), Is.EqualTo(10f));
            Assert.That(this.world.GetStash<MoveSpeedComponent>().Get(this.player).value, Is.EqualTo(8f));
        }

        [Test]
        public void RefreshExtendsLifetimeWithoutStackingAndInvalidatesOldHandle() {
            var buff = this.stats.Compile(BuiltInStatuses.CreateEmpowerment());
            var old = this.stats.Apply(this.player, this.source, buff);
            this.Advance(2f);
            var current = this.stats.Apply(this.player, this.source, buff);
            Assert.That(this.stats.Remove(old), Is.False);
            this.Advance(2f);
            Assert.That(this.Speed, Is.EqualTo(8.1f).Within(0.00001f));
            Assert.That(this.stats.ReadPlayer(this.player).RemainingSeconds, Is.EqualTo(1f));
            Assert.That(this.stats.ActiveCount, Is.EqualTo(1));
            Assert.That(this.stats.Remove(current), Is.True);
            Assert.That(this.stats.Remove(current), Is.False);
            this.stats.RecalculateDirty();
            Assert.That(this.Speed, Is.EqualTo(6f));
        }

        [Test]
        public void IndependentStacksExpireSeparatelyAndRejectOverflowWithoutMutation() {
            var buff = this.stats.Compile(new StatusEffectDefinition(2, 3f, StatusStacking.Independent, 2,
                new StatModifierDefinition(StatIds.MoveSpeed, StatOperation.Add, 2f)));
            this.stats.Apply(this.player, this.source, buff);
            this.Advance(1f);
            this.stats.Apply(this.player, this.source, buff);
            Assert.Throws<InvalidOperationException>(() => this.stats.Apply(this.player, this.source, buff));
            this.Advance(1f);
            Assert.That(this.Speed, Is.EqualTo(10f));
            this.Advance(1f);
            Assert.That(this.Speed, Is.EqualTo(8f));
            Assert.That(this.stats.ActiveCount, Is.EqualTo(1));
            this.Advance(1f);
            Assert.That(this.Speed, Is.EqualTo(6f));
        }

        [Test]
        public void CountdownDoesNotRecalculateAndMutationsCoalescePerActor() {
            var before = this.stats.RecalculationCount;
            this.stats.Apply(this.player, this.source, this.stats.Compile(BuiltInStatuses.CreateEmpowerment()));
            this.stats.SetBase(this.player, StatIds.MoveSpeed, 7f);
            this.stats.SetBase(this.player, StatIds.Armor, 20f);
            this.stats.RecalculateDirty();
            Assert.That(this.stats.RecalculationCount, Is.EqualTo(before + 1));
            for (var i = 0; i < 120; i++) this.Advance(GameSession.FixedStep);
            Assert.That(this.stats.RecalculationCount, Is.EqualTo(before + 1));
            this.stats.SetBase(this.player, StatIds.Armor, 20f);
            this.Advance(1f);
            Assert.That(this.stats.RecalculationCount, Is.EqualTo(before + 2));
            Assert.That(this.Speed, Is.EqualTo(7f));
        }

        [Test]
        public void RemovingWinningOverrideRevealsTheNextSourceThenBase() {
            var low = this.Status(2, 10f, new StatModifierDefinition(1, StatOperation.Override, 20f, 1));
            var high = this.Status(3, 1f, new StatModifierDefinition(1, StatOperation.Override, 30f, 2));
            var lowHandle = this.stats.Apply(this.player, this.source, low);
            this.stats.Apply(this.player, this.source, high);
            this.Advance(0f);
            Assert.That(this.Speed, Is.EqualTo(30f));
            this.Advance(1f);
            Assert.That(this.Speed, Is.EqualTo(20f));
            this.stats.Remove(lowHandle); this.stats.RecalculateDirty();
            Assert.That(this.Speed, Is.EqualTo(6f));
        }

        [Test]
        public void TargetDeathClearsEffectsButSourceDeathDoesNotOwnTargetLifetime() {
            var caster = this.Actor();
            var origin = new EffectSource(caster.ID, 7);
            var buff = this.stats.Compile(BuiltInStatuses.CreateEmpowerment());
            this.stats.Apply(this.player, origin, buff);
            this.world.RemoveEntity(caster); this.world.Commit();
            this.Advance(1f);
            Assert.That(this.stats.ActiveCount, Is.EqualTo(1));
            this.world.GetStash<HealthComponent>().Get(this.player).current = 0f;
            this.stats.ClearInvalidTargets(false); this.stats.RecalculateDirty();
            Assert.That(this.stats.ActiveCount, Is.Zero);
            Assert.That(this.Speed, Is.EqualTo(6f));
            Assert.Throws<InvalidOperationException>(() => this.stats.Apply(this.player, origin, buff));
        }

        [Test]
        public void EntityReuseCannotInheritEffectsOrAcceptStaleOwner() {
            this.stats.Apply(this.player, this.source, this.stats.Compile(BuiltInStatuses.CreateEmpowerment()));
            var stale = this.player;
            this.world.RemoveEntity(stale); this.world.Commit();
            this.player = this.Actor();
            this.Advance(0f);
            Assert.That(this.stats.ActiveCount, Is.Zero);
            Assert.That(this.Speed, Is.EqualTo(6f));
            Assert.Throws<ArgumentException>(() => this.stats.SetBase(stale, 1, 50f));
        }

        [Test]
        public void CapacityIsPerTargetAndFailureLeavesExistingHandlesUsable() {
            var buff = this.stats.Compile(BuiltInStatuses.CreateEmpowerment());
            var first = this.stats.Apply(this.player, this.source, buff);
            for (var i = 2; i <= StatStorage.StatusCapacityPerActor; i++)
                this.stats.Apply(this.player, new EffectSource(this.player.ID, i), buff);
            Assert.Throws<InvalidOperationException>(() => this.stats.Apply(this.player, new EffectSource(this.player.ID, 100), buff));
            var other = this.Actor();
            this.stats.Apply(other, this.source, buff);
            Assert.That(this.stats.ActiveCount, Is.EqualTo(17));
            Assert.That(this.stats.Remove(first), Is.True);
            this.stats.ClearInvalidTargets(true); this.stats.RecalculateDirty();
            Assert.That(this.stats.ActiveCount, Is.Zero);
            Assert.That(this.Speed, Is.EqualTo(6f));
        }

        [Test]
        public void NewCatalogStatWorksWithoutAddingAComponentAndUnknownIdsFailAtCompile() {
            var status = this.Status(9, 1f, new StatModifierDefinition(99, StatOperation.Add, 12f));
            this.stats.Apply(this.player, this.source, status); this.stats.RecalculateDirty();
            Assert.That(this.stats.GetFinal(this.player, 99), Is.EqualTo(17f));
            this.Advance(1f);
            Assert.That(this.stats.GetFinal(this.player, 99), Is.EqualTo(5f));
            Assert.Throws<ArgumentException>(() => this.Status(10, 1f, new StatModifierDefinition(999, StatOperation.Add, 1f)));
            Assert.Throws<ArgumentException>(() => this.Status(9, 2f, new StatModifierDefinition(99, StatOperation.Add, 12f)));
        }

        [Test]
        public void ForeignWorldAndSessionHandlesCannotMutateThisSession() {
            using var foreignWorld = World.Create("Foreign stat owner");
            foreignWorld.UpdateByUnity = false;
            var foreign = foreignWorld.CreateEntity();
            Assert.Throws<InvalidOperationException>(() => this.stats.Register(foreign, 1f, 1f));
            using var other = new StatStorage(StatCatalog.Default, 1);
            other.Attach(foreignWorld);
            foreignWorld.GetStash<HealthComponent>().Set(foreign, new HealthComponent { current = 100f, max = 100f });
            other.Register(foreign, 1f, 1f);
            var compiled = other.Compile(BuiltInStatuses.CreateEmpowerment());
            var handle = other.Apply(foreign, new EffectSource(foreign.ID, 1), compiled);
            Assert.That(this.stats.Remove(handle), Is.False);
            Assert.Throws<ArgumentException>(() => this.stats.Apply(this.player, this.source, compiled));
        }
    }

    public sealed class StatusSessionTests {
        [Test]
        public void AbilityChangesActualMovementExpiresAndNeverLeaksIntoRestart() {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try {
                config.obstaclePositions = Array.Empty<Vector3>();
                config.enemyTypes[0].damagePerSecond = 0f;
                var settings = config.CreateSimulationSettings();
                using (var session = new GameSession(settings)) {
                    for (var i = 0; i < 80; i++) session.TickUpdate(GameSession.FixedStep, new PlayerInputFrame { move = Vector2.up });
                    session.TickUpdate(GameSession.FixedStep, new PlayerInputFrame { abilityPressed = true });
                    Assert.That(session.Snapshot.playerStats.ActiveEffects, Is.EqualTo(1));
                    Assert.That(session.Snapshot.playerStats.Armor, Is.EqualTo(40f));
                    var before = session.PlayerPosition;
                    session.TickUpdate(GameSession.FixedStep, new PlayerInputFrame { move = Vector2.right });
                    Assert.That(session.PlayerPosition.x - before.x, Is.EqualTo(8.1f * GameSession.FixedStep).Within(0.00001f));
                    for (var i = 0; i < 178; i++) session.TickUpdate(GameSession.FixedStep, default);
                    Assert.That(session.Snapshot.playerStats.ActiveEffects, Is.EqualTo(1));
                    session.TickUpdate(GameSession.FixedStep, default);
                    Assert.That(session.Snapshot.playerStats.ActiveEffects, Is.Zero);
                    Assert.That(session.Snapshot.playerStats.MoveSpeed, Is.EqualTo(6f));
                    Assert.That(session.Snapshot.playerStats.Armor, Is.EqualTo(10f));
                    for (var i = 0; i < 122; i++) session.TickUpdate(GameSession.FixedStep, default);
                    session.TickUpdate(GameSession.FixedStep, new PlayerInputFrame { abilityPressed = true });
                    Assert.That(session.Snapshot.playerStats.ActiveEffects, Is.EqualTo(1));
                }
                using var restart = new GameSession(settings);
                Assert.That(restart.Snapshot.playerStats.ActiveEffects, Is.Zero);
                Assert.That(restart.Snapshot.playerStats.MoveSpeed, Is.EqualTo(6f));
                Assert.That(restart.Snapshot.playerStats.Armor, Is.EqualTo(10f));
            } finally { Object.DestroyImmediate(config); }
        }
    }
}

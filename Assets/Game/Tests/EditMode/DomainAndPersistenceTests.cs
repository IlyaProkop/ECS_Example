using System;
using System.IO;
using Game.Domain;
using Game.Progression;
using Game.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests {
    public sealed class DomainAndPersistenceTests {
        [Test]
        public void ModifiersApplyCriticalThenArmorAndCapOverkill() {
            var pipeline = new DamagePipeline(new CriticalDamageModifier(), new ArmorDamageModifier());
            var damage = new DamageCalculation { amount = 40f, armor = 100f, criticalChance = 1f, criticalMultiplier = 2f };
            Assert.That(pipeline.Resolve(ref damage, 200f), Is.EqualTo(40f));
            Assert.That(damage.isCritical, Is.True);
            damage.amount = 400f;
            Assert.That(pipeline.Resolve(ref damage, 10f), Is.EqualTo(10f));
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDamageDoesNotHealOrPoisonHealth(float value) {
            var damage = new DamageCalculation { amount = value };
            Assert.That(new DamagePipeline().Resolve(ref damage, 100f), Is.Zero);
        }

        [Test]
        public void SweptHitUsesSurfaceEntryInsteadOfClosestPoint() {
            Assert.That(Geometry2D.SegmentIntersectsCircle(Vector2.zero, Vector2.right * 10f, Vector2.right * 5f, 1f, out var hit), Is.True);
            Assert.That(hit, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void RewardsAreGrantedOnceAndOnlyForVictory() {
            var progress = new PlayerProgress();
            var stats = new BattleStatistics { duration = 20f, kills = 12 };
            var victory = new BattleResult("victory-1", BattleOutcome.Victory, stats, 100);
            Assert.That(progress.Apply(victory), Is.True);
            Assert.That(progress.Apply(victory), Is.False);
            progress.Apply(new BattleResult("defeat-2", BattleOutcome.Defeat, stats, 100));
            Assert.That(progress.currency, Is.EqualTo(100));
            Assert.That(progress.victories, Is.EqualTo(1));
            Assert.That(progress.battles, Is.EqualTo(2));
            Assert.That(progress.bestVictorySeconds, Is.EqualTo(20f));
        }

        [Test]
        public void SaveSurvivesRestartAndRecoversPreviousSnapshot() {
            var directory = Path.Combine(Path.GetTempPath(), "ArenaTests-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "progress.json");
            try {
                var store = new JsonProgressStore(path);
                Assert.That(store.Load().currency, Is.Zero);
                store.Save(new PlayerProgress { currency = 100, battles = 1, victories = 1 });
                Assert.That(new JsonProgressStore(path).Load().currency, Is.EqualTo(100));
                store.Save(new PlayerProgress { currency = 200, battles = 2, victories = 2 });
                File.WriteAllText(path, "{invalid json");
                Assert.That(store.Load().currency, Is.EqualTo(100));
                Assert.That(store.LoadWarning, Is.Not.Null.And.Not.Empty);
            } finally {
                // This test owns this unique temporary directory.
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}

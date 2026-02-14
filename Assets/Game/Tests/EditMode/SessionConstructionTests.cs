using System;
using Game.Config;
using Game.Domain;
using Game.ECS.Core;
using Game.Input;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class SessionConstructionTests {
        private GameConfig config;
        private SimulationSettings settings;
        [SetUp] public void SetUp() {
            this.config = ScriptableObject.CreateInstance<GameConfig>();
            this.settings = this.config.CreateSimulationSettings();
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(this.config);

        [Test]
        public void DeferredCreationPublishesOnceAndMatchesImmediateSimulation() {
            using var immediate = new GameSession(this.settings, captureRenderFrames: true);
            GameSession deferred = null;
            var calls = 0;
            using (var steps = GameSession.CreateSteps(this.settings, session => { calls++; deferred = session; }, captureRenderFrames: true)) {
                while (steps.MoveNext()) Assert.That(deferred, Is.Null, "A partial session must not escape.");
                Assert.That(steps.MoveNext(), Is.False);
                Assert.That(calls, Is.EqualTo(1));
            }
            using (deferred) {
                var left = new ActorView[immediate.ViewCapacity];
                var right = new ActorView[deferred.ViewCapacity];
                var input = new PlayerInputFrame { move = Vector2.up, attackHeld = true };
                for (var frame = 0; frame < 300; frame++) {
                    immediate.TickUpdate(GameSession.FixedStep, input);
                    deferred.TickUpdate(GameSession.FixedStep, input);
                    Assert.That(deferred.Snapshot, Is.EqualTo(immediate.Snapshot));
                    var count = immediate.CopyRenderActors(left);
                    Assert.That(deferred.CopyRenderActors(right), Is.EqualTo(count));
                    for (var i = 0; i < count; i++) Assert.That(right[i], Is.EqualTo(left[i]));
                }
            }
        }

        [Test]
        public void CancellationReleasesTheWorldAtEveryPreparationBoundary() {
            var count = 0;
            using (var complete = new SessionConstruction(this.settings, null, true))
                while (complete.MoveNext()) count++;
            Assert.That(count, Is.GreaterThan(1));
            for (var stop = 0; stop <= count; stop++) {
                using var construction = new SessionConstruction(this.settings, null, true);
                for (var step = 0; step < stop; step++) Assert.That(construction.MoveNext(), Is.True);
                var world = construction.World;
                construction.Dispose(); construction.Dispose();
                if (world != null) Assert.That(world.IsDisposed, Is.True, "Cancelled boundary " + stop);
                Assert.That(construction.World, Is.Null);
                Assert.That(construction.Context, Is.Null);
                Assert.Throws<ObjectDisposedException>(() => construction.MoveNext());
            }
        }

        [Test]
        public void DisposingPublicOperationCannotPublishLater() {
            var called = false;
            var operation = GameSession.CreateSteps(this.settings, _ => called = true);
            Assert.That(operation.MoveNext(), Is.True);
            Assert.That(operation.MoveNext(), Is.True);
            operation.Dispose();
            Assert.That(operation.MoveNext(), Is.False);
            Assert.That(called, Is.False);
        }

        [Test]
        public void CallbackFailureReleasesTheSession() {
            GameSession published = null;
            using var steps = GameSession.CreateSteps(this.settings, session => {
                published = session;
                throw new InvalidOperationException("Consumer rejected the ready session.");
            });
            Assert.Throws<InvalidOperationException>(() => { while (steps.MoveNext()) { } });
            Assert.That(published, Is.Not.Null);
            Assert.Throws<ObjectDisposedException>(() => published.TickUpdate(0, default));
        }
    }
}

using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Flow;
using NUnit.Framework;

namespace Game.Tests {
    public sealed class GameFlowLifetimeTests {
        [TestCase("tick", false)] [TestCase("tick", true)]
        [TestCase("result", false)] [TestCase("result", true)]
        [TestCase("record", false)] [TestCase("record", true)]
        [TestCase("current", false)] [TestCase("current", true)]
        [TestCase("status", false)] [TestCase("status", true)]
        [TestCase("show", false)] [TestCase("show", true)]
        public void ClosingInsideCallbackStopsFurtherWorkAndDefersDisposal(string boundary, bool dispose) {
            var fixture = new Fixture();
            using var flow = fixture.Flow;
            flow.AttachBattle(fixture, fixture);
            fixture.OnCall = stage => {
                if (stage != boundary) return;
                if (dispose) flow.Dispose(); else flow.DetachBattle(fixture);
                Assert.That(fixture.DisposeCount, Is.Zero, "The active callback still owns its stack/resources.");
                Assert.That(flow.Play(), Is.False);
                Assert.Throws<InvalidOperationException>(() => flow.AttachBattle(fixture, fixture));
            };
            fixture.Calls.Clear();
            flow.Tick(1f);
            var expected = new List<string>();
            foreach (var stage in new[] { "tick", "result", "record", "current", "status", "show" }) {
                expected.Add(stage);
                if (stage == boundary) break;
            }
            expected.Add("dispose");
            Assert.That(fixture.Calls, Is.EqualTo(expected));
            Assert.That(fixture.DisposeCount, Is.EqualTo(1));
            Assert.That(flow.State, Is.EqualTo(dispose ? GameFlowState.Disposed : GameFlowState.Menu));
            flow.Tick(1f);
            flow.DetachBattle(fixture);
            Assert.That(fixture.DisposeCount, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void RunReturnedAfterClosureIsDisposedWithoutResurrectingBattle(bool dispose) {
            var fixture = new Fixture();
            using var flow = fixture.Flow;
            fixture.OnCall = stage => {
                if (stage == "create") {
                    if (dispose) flow.Dispose(); else flow.DetachBattle(fixture);
                }
            };
            flow.AttachBattle(fixture, fixture);
            Assert.That(fixture.Calls, Is.EqualTo(new[] { "create", "dispose" }));
            Assert.That(flow.State, Is.EqualTo(dispose ? GameFlowState.Disposed : GameFlowState.Menu));
            Assert.That(fixture.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void SynchronousNavigationQueuesCreationUntilNavigationReturns() {
            var fixture = new Fixture();
            using var flow = fixture.Flow;
            fixture.OnCall = stage => {
                if (stage != "battle") return;
                flow.AttachBattle(fixture, fixture);
                Assert.That(fixture.Calls, Does.Not.Contain("create"));
                fixture.Calls.Add("navigation-return");
            };
            Assert.That(flow.Play(), Is.True);
            Assert.That(fixture.Calls, Is.EqualTo(new[] { "battle", "navigation-return", "create" }));
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Battle));
        }

        [Test]
        public void ClosingFromOldRunDisposalCancelsRestart() {
            var fixture = new Fixture();
            using var flow = fixture.Flow;
            flow.AttachBattle(fixture, fixture);
            flow.Tick(1f);
            fixture.Calls.Clear();
            fixture.OnCall = stage => { if (stage == "dispose") flow.Dispose(); };
            Assert.That(flow.Restart(), Is.False);
            Assert.That(fixture.Calls, Is.EqualTo(new[] { "dispose" }));
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Disposed));
        }

        [Test]
        public void RunAndCleanupErrorsBothSurviveAndRunIsDisposedOnce() {
            var fixture = new Fixture();
            using var flow = fixture.Flow;
            flow.AttachBattle(fixture, fixture);
            var runError = new InvalidOperationException("tick failure");
            var cleanupError = new InvalidOperationException("dispose failure");
            fixture.OnCall = stage => {
                if (stage == "tick") throw runError;
                if (stage == "dispose") throw cleanupError;
            };
            var error = Assert.Throws<AggregateException>(() => flow.Tick(1f));
            Assert.That(error.InnerExceptions, Is.EqualTo(new[] { runError, cleanupError }));
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Faulted));
            Assert.That(flow.ReturnToMenu(), Is.True);
            Assert.That(fixture.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void ErrorAfterDisposeDoesNotReplaceTerminalState() {
            var fixture = new Fixture();
            using var flow = fixture.Flow;
            flow.AttachBattle(fixture, fixture);
            var original = new InvalidOperationException("callback failed after closing");
            fixture.OnCall = stage => {
                if (stage != "tick") return;
                flow.Dispose();
                throw original;
            };
            Assert.That(Assert.Throws<InvalidOperationException>(() => flow.Tick(1f)), Is.SameAs(original));
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Disposed));
            Assert.That(fixture.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void DetachReleasesBindingEvenWhenRunDisposalFails() {
            var first = new Fixture();
            using var flow = first.Flow;
            flow.AttachBattle(first, first);
            first.OnCall = stage => { if (stage == "dispose") throw new InvalidOperationException("cleanup failed"); };
            Assert.Throws<InvalidOperationException>(() => flow.DetachBattle(first));
            flow.DetachBattle(first);
            Assert.That(first.DisposeCount, Is.EqualTo(1));
            var next = new Fixture();
            using var unusedFlow = next.Flow;
            flow.AttachBattle(next, next);
            flow.DetachBattle(first);
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Battle));
            Assert.That(next.DisposeCount, Is.Zero);
        }

        [Test]
        public void FailedSynchronousNavigationNeverCreatesTheAttachedRun() {
            var fixture = new Fixture();
            using var flow = fixture.Flow;
            fixture.OnCall = stage => {
                if (stage != "battle") return;
                flow.AttachBattle(fixture, fixture);
                throw new InvalidOperationException("navigation failed");
            };
            Assert.Throws<InvalidOperationException>(() => flow.Play());
            Assert.That(fixture.Calls, Is.EqualTo(new[] { "battle" }));
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Faulted));
            Assert.That(flow.ReturnToMenu(), Is.True);
        }

        private sealed class Fixture : IBattleFactory, IBattleRun, IGameNavigation, IProgressService, IResultView {
            public readonly List<string> Calls = new List<string>();
            public readonly GameFlowController Flow;
            public Action<string> OnCall;
            public int DisposeCount;
            public Fixture() => this.Flow = new GameFlowController(this, this);
            private void Call(string stage) { this.Calls.Add(stage); this.OnCall?.Invoke(stage); }
            public IBattleRun Create() { this.Call("create"); return this; }
            public void Tick(float deltaTime) => this.Call("tick");
            public bool TryGetResult(out BattleResult result) {
                this.Call("result");
                result = new BattleResult("lifetime-test", BattleOutcome.Victory, default, 100);
                return true;
            }
            public void Dispose() { this.DisposeCount++; this.Call("dispose"); }
            public void OpenBattle() => this.Call("battle");
            public void OpenMenu() => this.Call("menu");
            public PlayerProgress Current { get { this.Call("current"); return new PlayerProgress(); } }
            public string Status { get { this.Call("status"); return string.Empty; } }
            public void Record(in BattleResult result) => this.Call("record");
            public void Flush() => this.Call("flush");
            public void ShowResult(in BattleResult result, PlayerProgress progress, string saveStatus) => this.Call("show");
        }
    }
}

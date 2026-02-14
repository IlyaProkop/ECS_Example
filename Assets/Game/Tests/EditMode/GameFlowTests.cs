using System;
using System.Collections.Generic;
using System.IO;
using Game.Domain;
using Game.Flow;
using Game.Progression;
using NUnit.Framework;

namespace Game.Tests {
    public sealed class GameFlowTests {
        [Test]
        public void CompleteRestartAndMenuKeepOneOwnerAndRecordEachResultOnce() {
            var calls = new List<string>();
            var store = new Store();
            var progress = new ProgressService(store);
            var navigation = new Navigation(calls);
            var battles = new Battles(calls);
            var view = new Results();
            using var flow = new GameFlowController(progress, navigation);
            Assert.That(flow.Play(), Is.True);
            Assert.That(flow.Play(), Is.False);
            flow.AttachBattle(battles, view);
            Assert.That(flow.Restart(), Is.False, "An unfinished battle cannot be replaced through Results commands.");
            battles.current.finished = true;
            flow.Tick(1f);
            flow.Tick(1f);
            Assert.That(progress.Current.currency, Is.EqualTo(100));
            Assert.That(progress.Current.battles, Is.EqualTo(1));
            Assert.That(view.count, Is.EqualTo(1));
            var first = battles.current;
            Assert.That(flow.Restart(), Is.True);
            Assert.That(first.disposeCount, Is.EqualTo(1));
            Assert.That(calls.IndexOf("dispose-1"), Is.LessThan(calls.IndexOf("create-2")));
            Assert.That(flow.ReturnToMenu(), Is.True);
            Assert.That(flow.ReturnToMenu(), Is.False);
            Assert.That(calls.IndexOf("dispose-2"), Is.LessThan(calls.IndexOf("menu")));
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Menu));
            Assert.That(progress.Current.battles, Is.EqualTo(1), "Aborting a run does not fabricate a battle result.");
        }

        [Test]
        public void SaveFailureRetriesOnMenuWithoutGrantingRewardAgain() {
            var store = new Store { fail = true };
            var progress = new ProgressService(store);
            var battles = new Battles(new List<string>());
            var view = new Results();
            using var flow = new GameFlowController(progress, new Navigation(new List<string>()));
            flow.AttachBattle(battles, view);
            battles.current.finished = true;
            flow.Tick(1f);
            Assert.That(progress.HasPendingSave, Is.True);
            Assert.That(view.progress.currency, Is.EqualTo(100));
            Assert.That(view.status, Does.Contain("Ошибка"));
            store.fail = false;
            flow.ReturnToMenu();
            Assert.That(store.saved.currency, Is.EqualTo(100));
            Assert.That(store.saved.battles, Is.EqualTo(1));
            Assert.That(progress.HasPendingSave, Is.False);
        }

        [Test]
        public void ResultCallbackCannotReenterRestartOrRecordAgain() {
            var progress = new ProgressService(new Store());
            var battles = new Battles(new List<string>());
            using var flow = new GameFlowController(progress, new Navigation(new List<string>()));
            var view = new Results { onShow = () => {
                Assert.That(flow.Restart(), Is.False);
                Assert.That(flow.ReturnToMenu(), Is.False);
                flow.Tick(1f);
            } };
            flow.AttachBattle(battles, view);
            battles.current.finished = true;
            flow.Tick(1f);
            Assert.That(view.count, Is.EqualTo(1));
            Assert.That(progress.Current.battles, Is.EqualTo(1));
            Assert.That(flow.Restart(), Is.True, "The same command is accepted after callbacks return.");
        }

        [Test]
        public void OldSceneDetachCannotStopTheNewScene() {
            var first = new Battles(new List<string>());
            var second = new Battles(new List<string>());
            using var flow = new GameFlowController(new ProgressService(new Store()), new Navigation(new List<string>()));
            flow.AttachBattle(first, new Results());
            flow.ReturnToMenu();
            flow.Play();
            flow.AttachBattle(second, new Results());
            flow.DetachBattle(first);
            Assert.That(second.current.disposeCount, Is.Zero);
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Battle));
            Assert.Throws<InvalidOperationException>(() => flow.AttachBattle(first, new Results()));
            flow.DetachBattle(second);
            flow.DetachBattle(second);
            Assert.That(second.current.disposeCount, Is.EqualTo(1));
        }

        [Test]
        public void FailedRunIsDisposedAndMenuRemainsAvailable() {
            var battles = new Battles(new List<string>());
            using var flow = new GameFlowController(new ProgressService(new Store()), new Navigation(new List<string>()));
            flow.AttachBattle(battles, new Results());
            battles.current.fail = true;
            Assert.Throws<InvalidOperationException>(() => flow.Tick(1f));
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Faulted));
            Assert.That(battles.current.disposeCount, Is.EqualTo(1));
            flow.Tick(1f);
            Assert.That(flow.ReturnToMenu(), Is.True);
            Assert.That(battles.current.disposeCount, Is.EqualTo(1));
        }

        [Test]
        public void FactoryFailureAndDisposalDoNotPermitAnotherRunImplicitly() {
            var battles = new Battles(new List<string>()) { fail = true };
            var flow = new GameFlowController(new ProgressService(new Store()), new Navigation(new List<string>()));
            Assert.Throws<InvalidOperationException>(() => flow.AttachBattle(battles, new Results()));
            Assert.That(flow.State, Is.EqualTo(GameFlowState.Faulted));
            Assert.That(flow.Restart(), Is.False);
            Assert.That(flow.ReturnToMenu(), Is.True);
            flow.Dispose();
            flow.Dispose();
            Assert.That(flow.Play(), Is.False);
            Assert.Throws<InvalidOperationException>(() => flow.AttachBattle(battles, new Results()));
        }

        private sealed class Store : IProgressStore {
            public bool fail;
            public PlayerProgress saved;
            public PlayerProgress Load() => new PlayerProgress();
            public void Save(PlayerProgress progress) {
                if (this.fail) throw new IOException("Storage unavailable");
                this.saved = progress;
            }
        }
        private sealed class Navigation : IGameNavigation {
            private readonly List<string> calls;
            public Navigation(List<string> calls) => this.calls = calls;
            public void OpenBattle() => this.calls.Add("battle");
            public void OpenMenu() => this.calls.Add("menu");
        }
        private sealed class Battles : IBattleFactory {
            private readonly List<string> calls;
            private int sequence;
            public Run current;
            public bool fail;
            public Battles(List<string> calls) => this.calls = calls;
            public IBattleRun Create() {
                if (this.fail) throw new InvalidOperationException("Failed to create run");
                this.calls.Add($"create-{++this.sequence}");
                return this.current = new Run(this.sequence, this.calls);
            }
        }
        private sealed class Run : IBattleRun {
            private readonly int id;
            private readonly List<string> calls;
            public bool finished, fail;
            public int disposeCount;
            public Run(int id, List<string> calls) { this.id = id; this.calls = calls; }
            public void Tick(float deltaTime) {
                if (this.fail) throw new InvalidOperationException("Run failed");
                Assert.That(this.disposeCount, Is.Zero);
            }
            public bool TryGetResult(out BattleResult result) {
                result = new BattleResult($"battle-{this.id}", BattleOutcome.Victory, new BattleStatistics { duration = 12f }, 100);
                return this.finished;
            }
            public void Dispose() { this.disposeCount++; this.calls.Add($"dispose-{this.id}"); }
        }
        private sealed class Results : IResultView {
            public int count;
            public Action onShow;
            public PlayerProgress progress;
            public string status;
            public void ShowResult(in BattleResult result, PlayerProgress progress, string saveStatus) {
                this.count++; this.progress = progress; this.status = saveStatus;
                this.onShow?.Invoke();
            }
        }
    }
}

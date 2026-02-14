using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Flow;
using Game.UI.Binding;
using Game.UiModel;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests {
    public sealed class UiModelTests {
        [Test]
        public void UiModelsHaveNoUnityEcsAuthoringOrPresentationDependency() {
            var names = typeof(HudViewModel).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(names.Any(n => n.StartsWith("Unity") || n == "Game.ECS" || n == "Game.Authoring" || n == "Game.Presentation"), Is.False);
            Assert.That(names, Does.Contain("Game.Application"));
            Assert.That(names, Does.Contain("Game.Domain"));
        }

        [Test]
        public void HudPublishesCompleteUpdatesAtDisplayedPrecision() {
            using var model = new HudViewModel("Импульс");
            var changes = 0;
            model.Changed += () => { changes++; Assert.That(model.Coins, Is.Not.Empty); Assert.That(model.Objective, Is.Not.Empty); };
            model.Update(Snapshot(100, 0, 0.01f, 4.98f));
            model.Update(Snapshot(100, 0, 0.02f, 4.97f));
            Assert.That(changes, Is.EqualTo(1), "No notification or string reconstruction for sub-display changes.");
            model.Update(Snapshot(25, 2, 61f, 0f));
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(model.Health, Does.Contain("25"));
            Assert.That(model.Coins, Does.Contain("2"));
            Assert.That(model.Objective, Does.Contain("01:01"));
            Assert.That(model.LowHealth, Is.True);
            Assert.That(model.Controls, Does.Contain("Импульс"));
        }

        [Test]
        public void HudEffectsUseSnapshotPrecisionAndDisappearWhenTheStatusEnds() {
            using var model = new HudViewModel("Волна");
            var changes = 0;
            model.Changed += () => changes++;
            model.Update(EffectSnapshot(1, 2.98f));
            var text = model.StatusEffects;
            model.Update(EffectSnapshot(1, 2.97f));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(model.StatusEffects, Is.SameAs(text));
            Assert.That(model.HasStatusEffects, Is.True);
            Assert.That(model.StatusEffects, Does.Contain("БРОНЯ 40").And.Contain("СКОРОСТЬ"));
            model.Update(EffectSnapshot(1, 2.8f));
            Assert.That(changes, Is.EqualTo(2));
            model.Update(EffectSnapshot(0, 0f));
            Assert.That(model.HasStatusEffects, Is.False);
            Assert.That(model.StatusEffects, Is.Empty);
            Assert.That(changes, Is.EqualTo(3));
        }
        private static BattleSnapshot EffectSnapshot(int count, float time) => new BattleSnapshot(100, 0, 1,
            SessionPhase.Combat, BattleOutcome.None, default, 0f, Vector3.zero,
            new Game.Domain.Stats.PlayerStatsSnapshot(count, time, 8.1f, 40f));

        [Test]
        public void ResultModelDisplaysProvidedRewardAndGuardsNavigation() {
            var commands = new Commands();
            using var model = new ResultsViewModel(commands);
            Assert.That(model.Visible, Is.False);
            Assert.That(model.Restart.TryExecute(), Is.False);
            var progress = new PlayerProgress { currency = 500, victories = 3, battles = 4 };
            model.ShowResult(new BattleResult("ui-result", BattleOutcome.Victory, new BattleStatistics { kills = 12 }, 137), progress, "saved");
            progress.currency = 999;
            Assert.That(model.Details, Does.Contain("+137").And.Contain("500").And.Not.Contain("999"));
            Assert.That(model.Visible && model.Victory, Is.True);
            commands.Accept = false;
            Assert.That(model.Restart.TryExecute(), Is.False);
            Assert.That(model.Restart.CanExecute, Is.True, "A rejected transition can be retried.");
            commands.Accept = true;
            Assert.That(model.Menu.TryExecute(), Is.True);
            Assert.That(model.Restart.TryExecute(), Is.False);
            Assert.That(model.Menu.TryExecute(), Is.False);
            Assert.That(commands.Restarts, Is.EqualTo(1));
            Assert.That(commands.Menus, Is.EqualTo(1));
        }

        [Test]
        public void MenuRefreshReadsProgressWithoutRecordingOrSaving() {
            var progress = new Progress();
            var commands = new Commands();
            using var model = new MenuViewModel(progress, commands, "Рывок");
            var changes = 0;
            model.Changed += () => changes++;
            model.Refresh();
            model.Refresh();
            Assert.That(changes, Is.EqualTo(1));
            progress.Value.currency = 250;
            progress.Message = "save failed";
            model.Refresh();
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(model.Summary, Does.Contain("250"));
            Assert.That(model.SaveStatus, Is.EqualTo("save failed"));
            Assert.That(model.Instructions, Does.Contain("Рывок"));
            Assert.That(progress.Writes, Is.Zero);
            Assert.That(model.Play.TryExecute(), Is.True);
            Assert.That(model.Play.TryExecute(), Is.False);
            Assert.That(commands.Plays, Is.EqualTo(1));
        }

        [Test]
        public void CommandRejectsReentryAndRecoversAfterFailure() {
            UiCommand command = null;
            var calls = 0;
            var states = new List<bool>();
            command = new UiCommand(() => {
                calls++;
                Assert.That(command.TryExecute(), Is.False);
                if (calls == 1) throw new InvalidOperationException("operation failed");
                return true;
            });
            using (command) {
                command.CanExecuteChanged += () => states.Add(command.CanExecute);
                Assert.Throws<InvalidOperationException>(() => command.TryExecute());
                Assert.That(command.CanExecute, Is.True);
                Assert.That(command.TryExecute(), Is.True);
                Assert.That(calls, Is.EqualTo(2));
                Assert.That(states, Is.EqualTo(new[] { false, true, false, true }));
            }
        }

        [Test]
        public void CommandCanCloseDuringCanExecuteNotificationOrAction() {
            var calls = 0;
            var canceled = new UiCommand(() => { calls++; return true; });
            canceled.CanExecuteChanged += () => canceled.Dispose();
            Assert.That(canceled.TryExecute(), Is.False);
            Assert.That(calls, Is.Zero);
            UiCommand selfClosing = null;
            selfClosing = new UiCommand(() => { selfClosing.Dispose(); return true; });
            Assert.That(selfClosing.TryExecute(), Is.True);
            Assert.That(selfClosing.TryExecute(), Is.False);
            Assert.That(selfClosing.CanExecute, Is.False);
        }

        [Test]
        public void DisposalPreventsOldScreenCommandsAndUpdates() {
            var commands = new Commands();
            var menu = new MenuViewModel(new Progress(), commands, "Волна");
            var result = new ResultsViewModel(commands);
            result.ShowResult(new BattleResult("old", BattleOutcome.Defeat, default, 0), new PlayerProgress(), "");
            menu.Dispose(); result.Dispose();
            Assert.That(menu.Play.TryExecute(), Is.False);
            Assert.That(result.Restart.TryExecute(), Is.False);
            Assert.That(result.Menu.TryExecute(), Is.False);
            Assert.Throws<ObjectDisposedException>(() => menu.Refresh());
            Assert.That(commands.Plays + commands.Restarts + commands.Menus, Is.Zero);
        }

        [Test]
        public void TypedBindingsSkipUnchangedWritesAndDetachOnDispose() {
            using var state = new HudViewModel("Волна");
            var bindings = new ViewBindings();
            var writes = 0;
            bindings.Property(() => state.Health, _ => writes++);
            bindings.Observe(state);
            bindings.Refresh();
            state.Update(Snapshot(100, 0, 0f, 0f));
            state.Update(Snapshot(100, 1, 1f, 0f));
            Assert.That(writes, Is.EqualTo(2), "Other properties changing must not rewrite this text.");
            bindings.Dispose(); bindings.Dispose();
            state.Update(Snapshot(10, 1, 2f, 0f));
            Assert.That(writes, Is.EqualTo(2));
        }

        [Test]
        public void BindingDispatchStopsIfAnEarlierBindingClosesItsScope() {
            var bindings = new ViewBindings();
            var laterWrites = 0;
            bindings.Property(() => 1, _ => bindings.Dispose());
            bindings.Property(() => 2, _ => laterWrites++);
            bindings.Refresh();
            Assert.That(laterWrites, Is.Zero);
        }

        private static BattleSnapshot Snapshot(int health, int coins, float seconds, float cooldown) =>
            new BattleSnapshot(health, coins, 3, SessionPhase.Combat, BattleOutcome.None,
                new BattleStatistics { duration = seconds }, cooldown, Vector3.zero);
        private sealed class Commands : IGameCommands {
            public int Plays, Restarts, Menus;
            public bool Accept = true;
            public bool Play() { this.Plays++; return this.Accept; }
            public bool Restart() { this.Restarts++; return this.Accept; }
            public bool ReturnToMenu() { this.Menus++; return this.Accept; }
        }
        private sealed class Progress : IProgressService {
            public readonly PlayerProgress Value = new PlayerProgress();
            public string Message = string.Empty;
            public int Writes;
            public PlayerProgress Current => this.Value.Copy();
            public string Status => this.Message;
            public void Record(in BattleResult result) => this.Writes++;
            public void Flush() => this.Writes++;
        }
    }
}

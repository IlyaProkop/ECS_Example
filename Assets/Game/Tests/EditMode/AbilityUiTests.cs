using System;
using Game.Domain.Abilities;
using Game.UiModel;
using NUnit.Framework;

namespace Game.Tests {
    public sealed class AbilityUiTests {
        private sealed class Commands : IAbilityCommands {
            public int Calls, Last = -1;
            public bool TryActivateAbility(int slot) { this.Calls++; this.Last = slot; return true; }
        }
        [Test]
        public void SlotsPublishIndependentCooldownsAndRouteCommandsBySlot() {
            var commands = new Commands();
            using var bar = new AbilityBarViewModel(new[] { "A", "B", "C", "D" }, commands);
            var snapshot = new AbilityBarSnapshot(4, new AbilitySlotSnapshot(1, 5f, 0f, true),
                new AbilitySlotSnapshot(2, 10f, 5f, false), new AbilitySlotSnapshot(3, 2f, 0f, true),
                new AbilitySlotSnapshot(4, 1f, 0f, false));
            bar.Update(snapshot);
            Assert.That(bar.Slots[1].CooldownFraction, Is.EqualTo(0.5f));
            Assert.That(bar.Slots[1].Activate.TryExecute(), Is.False);
            Assert.That(bar.Slots[3].Activate.TryExecute(), Is.False);
            Assert.That(bar.Slots[2].Activate.TryExecute(), Is.True);
            Assert.That(commands.Last, Is.EqualTo(2));
            var changes = 0; bar.Slots[0].Changed += () => changes++;
            bar.Update(snapshot);
            Assert.That(changes, Is.Zero, "Unchanged slots must not rewrite UI state.");
            bar.Update(default);
            Assert.That(bar.Slots[0].Activate.CanExecute, Is.False);
            bar.Dispose();
            Assert.That(bar.Slots[2].Activate.TryExecute(), Is.False);
            Assert.That(commands.Calls, Is.EqualTo(1));
        }
        [Test]
        public void SnapshotAndNamesCannotBeMutatedThroughRetainedInputs() {
            var names = new[] { "Original" };
            using var bar = new AbilityBarViewModel(names, new Commands());
            names[0] = "Changed";
            var snapshot = new AbilityBarSnapshot(1, new AbilitySlotSnapshot(7, 4f, 2f, false));
            var copy = snapshot;
            snapshot = default;
            bar.Update(copy);
            Assert.That(copy[0].Id, Is.EqualTo(7));
            Assert.That(bar.Slots[0].Label, Does.Contain("Original"));
            Assert.Throws<ArgumentOutOfRangeException>(() => { var unused = copy[1]; });
        }
    }
}

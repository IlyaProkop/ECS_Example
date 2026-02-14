using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Game.Domain.Abilities;

namespace Game.UiModel {
    public interface IAbilitySlotState : IViewState {
        string Label { get; }
        float CooldownFraction { get; }
        IUiCommand Activate { get; }
    }
    public sealed class AbilitySlotViewModel : ViewModel, IAbilitySlotState {
        private readonly string name, key;
        private readonly UiCommand activate;
        private int lastTenths = -1, lastId = -1;
        private bool lastReady;
        public string Label { get; private set; } = string.Empty;
        public float CooldownFraction { get; private set; }
        public IUiCommand Activate => this.activate;
        public AbilitySlotViewModel(int slot, string name, IAbilityCommands commands) {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            this.name = UiText.RequireAbilityName(name);
            this.key = slot == 0 ? "1 / Space / E" : (slot + 1).ToString();
            this.activate = new UiCommand(() => commands.TryActivateAbility(slot), false);
        }
        public void Update(in AbilitySlotSnapshot snapshot) {
            this.ThrowIfDisposed();
            var tenths = (int)Math.Ceiling(Math.Max(0f, snapshot.Remaining) * 10f);
            var fraction = snapshot.Cooldown > 0f ? Math.Min(1f, Math.Max(0f, snapshot.Remaining / snapshot.Cooldown)) : 0f;
            var changed = this.CooldownFraction != fraction;
            this.CooldownFraction = fraction;
            if (this.lastTenths != tenths || this.lastReady != snapshot.CanActivate || this.lastId != snapshot.Id) {
                var status = tenths > 0 ? $"{tenths / 10f:0.0} с" : snapshot.CanActivate ? "ГОТОВО" : "НЕДОСТУПНО";
                this.Label = $"{this.key}  {this.name}\n{status}";
                this.lastTenths = tenths; this.lastReady = snapshot.CanActivate; this.lastId = snapshot.Id;
                changed = true;
            }
            this.activate.SetEnabled(snapshot.Id > 0 && snapshot.CanActivate);
            if (changed) this.NotifyChanged();
        }
        protected override void DisposeOwned() => this.activate.Dispose();
    }
    public interface IAbilityBarState : IViewState { IReadOnlyList<AbilitySlotViewModel> Slots { get; } }
    public sealed class AbilityBarViewModel : ViewModel, IAbilityBarState {
        public IReadOnlyList<AbilitySlotViewModel> Slots { get; }
        public AbilityBarViewModel(IReadOnlyList<string> names, IAbilityCommands commands) {
            if (names == null || names.Count > AbilityLoadout.MaxSlots) throw new ArgumentException("Invalid ability names.");
            var slots = new AbilitySlotViewModel[names.Count];
            try { for (var i = 0; i < slots.Length; i++) slots[i] = new AbilitySlotViewModel(i, names[i], commands); }
            catch { foreach (var slot in slots) slot?.Dispose(); throw; }
            this.Slots = Array.AsReadOnly(slots);
        }
        public void Update(in AbilityBarSnapshot snapshot) {
            this.ThrowIfDisposed();
            for (var i = 0; i < this.Slots.Count; i++) this.Slots[i].Update(i < snapshot.Count ? snapshot[i] : default);
        }
        protected override void DisposeOwned() { foreach (var slot in this.Slots) slot.Dispose(); }
    }
}

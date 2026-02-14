using System;
using Game.Domain;

namespace Game.UiModel {
    public interface IHudState : IViewState {
        string Health { get; }
        string Coins { get; }
        string Objective { get; }
        string Controls { get; }
        string StatusEffects { get; }
        bool HasStatusEffects { get; }
        bool LowHealth { get; }
    }

    public sealed class HudViewModel : ViewModel, IHudState {
        private int lastHealth = -1, lastCoins = -1, lastSeconds = -1, lastEnemies = -1;
        private SessionPhase lastPhase = (SessionPhase)(-1);
        private int lastEffectCount = -1, lastEffectTime = -1, lastSpeed = -1, lastArmor = -1;
        public string Health { get; private set; } = string.Empty;
        public string Coins { get; private set; } = string.Empty;
        public string Objective { get; private set; } = string.Empty;
        public string Controls { get; }
        public string StatusEffects { get; private set; } = string.Empty;
        public bool HasStatusEffects { get; private set; }
        public bool LowHealth { get; private set; }

        public HudViewModel(string abilityName) {
            UiText.RequireAbilityName(abilityName);
            this.Controls = UiText.Controls(abilityName);
        }
        public void Update(in BattleSnapshot snapshot) {
            this.ThrowIfDisposed();
            var changed = false;
            if (this.lastHealth != snapshot.health) {
                this.lastHealth = snapshot.health;
                this.Health = $"ЗДОРОВЬЕ  {snapshot.health}";
                this.LowHealth = snapshot.health <= 25;
                changed = true;
            }
            if (this.lastCoins != snapshot.coins) {
                this.lastCoins = snapshot.coins;
                this.Coins = $"МОНЕТЫ  {snapshot.coins}";
                changed = true;
            }
            var seconds = (int)Math.Floor(snapshot.statistics.duration);
            if (this.lastPhase != snapshot.phase || this.lastEnemies != snapshot.enemiesRemaining || this.lastSeconds != seconds) {
                var objective = snapshot.phase switch {
                    SessionPhase.Initialization => "Инициализация...",
                    SessionPhase.AwaitingEntry => "Войдите на арену через зелёный проход (W / ↑)",
                    SessionPhase.Combat => $"Врагов осталось: {snapshot.enemiesRemaining}     Время: {seconds / 60:00}:{seconds % 60:00}",
                    SessionPhase.Results => "Подсчёт результатов...",
                    _ => "Бой завершён"
                };
                changed |= this.Objective != objective;
                this.Objective = objective;
                this.lastPhase = snapshot.phase;
                this.lastEnemies = snapshot.enemiesRemaining;
                this.lastSeconds = seconds;
            }
            var stats = snapshot.playerStats;
            var effectTime = (int)Math.Ceiling(stats.RemainingSeconds * 10f);
            var speed = (int)Math.Round(stats.MoveSpeed * 10f);
            var armor = (int)Math.Round(stats.Armor * 10f);
            if (this.lastEffectCount != stats.ActiveEffects || this.lastEffectTime != effectTime || this.lastSpeed != speed || this.lastArmor != armor) {
                this.HasStatusEffects = stats.ActiveEffects > 0;
                var text = this.HasStatusEffects
                    ? $"ЭФФЕКТЫ: {stats.ActiveEffects}   до {effectTime / 10f:0.0} с   СКОРОСТЬ {speed / 10f:0.0}   БРОНЯ {armor / 10f:0.#}"
                    : string.Empty;
                changed |= this.StatusEffects != text;
                this.StatusEffects = text;
                this.lastEffectCount = stats.ActiveEffects; this.lastEffectTime = effectTime;
                this.lastSpeed = speed; this.lastArmor = armor;
            }
            if (changed) this.NotifyChanged();
        }
    }
}

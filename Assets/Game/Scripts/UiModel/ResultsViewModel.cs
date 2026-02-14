using System;
using Game.Domain;
using Game.Flow;

namespace Game.UiModel {
    public interface IResultsState : IViewState {
        bool Visible { get; }
        bool Victory { get; }
        string Title { get; }
        string Details { get; }
        IUiCommand Restart { get; }
        IUiCommand Menu { get; }
    }

    public sealed class ResultsViewModel : ViewModel, IResultsState, IResultView {
        private readonly UiCommand restart, menu;
        public bool Visible { get; private set; }
        public bool Victory { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Details { get; private set; } = string.Empty;
        public IUiCommand Restart => this.restart;
        public IUiCommand Menu => this.menu;
        public ResultsViewModel(IGameCommands commands) {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            this.restart = new UiCommand(() => this.Navigate(commands.Restart), false);
            this.menu = new UiCommand(() => this.Navigate(commands.ReturnToMenu), false);
        }
        private bool Navigate(Func<bool> action) {
            if (!action()) return false;
            this.restart.SetEnabled(false);
            this.menu.SetEnabled(false);
            return true;
        }
        public void ShowResult(in BattleResult result, PlayerProgress progress, string saveStatus) {
            this.ThrowIfDisposed();
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            this.Victory = result.outcome == BattleOutcome.Victory;
            this.Title = this.Victory ? "ПОБЕДА" : "ПОРАЖЕНИЕ";
            var stats = result.statistics;
            this.Details = $"Время боя: {stats.duration:0.0} с\n" +
                $"Уничтожено врагов: {stats.kills}\n" +
                $"Нанесено / получено урона: {stats.damageDealt:0.#} / {stats.damageReceived:0.#}\n" +
                $"Базовых атак: {stats.attacks}   Критических попаданий: {stats.criticalHits}\n" +
                $"Применений способности: {stats.abilityUses}   Собрано монет: {stats.coinsCollected}\n\n" +
                $"Награда: +{result.reward}   Всего валюты: {progress.currency}\n" +
                $"Побед: {progress.victories} / {progress.battles}\n{saveStatus}";
            this.Visible = true;
            this.restart.SetEnabled(true);
            this.menu.SetEnabled(true);
            this.NotifyChanged();
        }
        protected override void DisposeOwned() {
            try { this.restart.Dispose(); }
            finally { this.menu.Dispose(); }
        }
    }
}

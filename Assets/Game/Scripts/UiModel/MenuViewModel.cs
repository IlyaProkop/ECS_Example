using System;
using Game.Flow;

namespace Game.UiModel {
    public interface IMenuState : IViewState {
        string Summary { get; }
        string Instructions { get; }
        string SaveStatus { get; }
        IUiCommand Play { get; }
    }
    public sealed class MenuViewModel : ViewModel, IMenuState {
        private readonly IProgressService progress;
        private readonly UiCommand play;
        public string Summary { get; private set; } = string.Empty;
        public string SaveStatus { get; private set; } = string.Empty;
        public string Instructions { get; }
        public IUiCommand Play => this.play;
        public MenuViewModel(IProgressService progress, IGameCommands commands, string abilityName) {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            this.Instructions = "Войдите на арену и уничтожьте всех врагов. Награда выдаётся за победу.\n" +
                UiText.Controls(UiText.RequireAbilityName(abilityName));
            this.play = new UiCommand(() => {
                if (!commands.Play()) return false;
                this.play.SetEnabled(false);
                return true;
            });
        }
        public void Refresh() {
            this.ThrowIfDisposed();
            var current = this.progress.Current;
            var best = current.victories > 0 ? $"{current.bestVictorySeconds:0.0} с" : "—";
            var summary = $"Валюта: {current.currency}   Побед: {current.victories} / {current.battles}\n" +
                $"Уничтожено врагов: {current.totalKills}   Лучшее время: {best}";
            var status = this.progress.Status ?? string.Empty;
            if (this.Summary == summary && this.SaveStatus == status) return;
            this.Summary = summary;
            this.SaveStatus = status;
            this.NotifyChanged();
        }
        protected override void DisposeOwned() => this.play.Dispose();
    }
}

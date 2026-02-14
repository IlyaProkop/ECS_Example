using System;
using Game.Domain;
using Game.Flow;
using Game.UiModel;

namespace Game.UI {
    // One run's screen state and bindings; scene views can be reused for the next run.
    internal sealed class BattleUiSession : IDisposable, IResultView {
        private readonly HudView hudView;
        private readonly ResultsView resultsView;
        private readonly HudViewModel hud;
        private readonly ResultsViewModel results;
        private readonly AbilityBarViewModel abilities;
        private bool disposed;
        public BattleUiSession(HudView hudView, ResultsView resultsView, IGameCommands commands,
            string abilityName, in BattleSnapshot initialSnapshot, System.Collections.Generic.IReadOnlyList<string> abilityNames, Game.Domain.Abilities.IAbilityCommands abilityCommands) {
            this.hudView = hudView; this.resultsView = resultsView;
            this.hud = new HudViewModel(abilityName);
            this.results = new ResultsViewModel(commands);
            try {
                this.abilities = new AbilityBarViewModel(abilityNames, abilityCommands);
                this.abilities.Update(initialSnapshot.abilities);
                this.hudView.Abilities.Bind(this.abilities);
                this.hud.Update(initialSnapshot);
                this.hudView.Bind(this.hud);
                this.resultsView.Bind(this.results);
            } catch { this.Dispose(); throw; }
        }
        public void Update(in BattleSnapshot snapshot) { this.hud.Update(snapshot); this.abilities.Update(snapshot.abilities); }
        public void ShowResult(in BattleResult result, PlayerProgress progress, string saveStatus) =>
            this.results.ShowResult(result, progress, saveStatus);
        public void Dispose() {
            if (this.disposed) return;
            this.disposed = true;
            try {
                if (this.hudView != null) {
                    this.hudView.Unbind(this.hud);
                    if (this.hudView.Abilities != null) this.hudView.Abilities.Unbind(this.abilities);
                }
                if (this.resultsView != null) this.resultsView.Unbind(this.results);
            } finally {
                this.abilities?.Dispose();
                try { this.hud.Dispose(); }
                finally { this.results.Dispose(); }
            }
        }
    }
}

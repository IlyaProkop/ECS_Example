using System;
using System.Runtime.ExceptionServices;

namespace Game.Flow {
    public enum GameFlowState { Menu, LoadingBattle, Battle, Results, Faulted, Disposed }

    // Main-thread application policy. Unity creates and renders a run through adapters.
    public sealed class GameFlowController : IGameCommands, IDisposable {
        private readonly IProgressService progress;
        private readonly IGameNavigation navigation;
        private IBattleFactory factory;
        private IResultView resultView;
        private IBattleRun run;
        private bool executing;
        private bool openingBattle;
        private bool stopRequested;
        public GameFlowState State { get; private set; } = GameFlowState.Menu;

        public GameFlowController(IProgressService progress, IGameNavigation navigation) {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        public bool Play() {
            if (this.executing || this.State != GameFlowState.Menu) return false;
            return this.Execute(() => {
                this.State = GameFlowState.LoadingBattle;
                this.openingBattle = true;
                try { this.navigation.OpenBattle(); }
                finally { this.openingBattle = false; }
                // Navigation may attach a scene synchronously. Create only after it returns.
                if (!this.stopRequested && this.factory != null) this.CreateRun();
            });
        }

        // Called by the loaded scene; Menu also permits opening the battle scene directly.
        public void AttachBattle(IBattleFactory factory, IResultView resultView) {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (resultView == null) throw new ArgumentNullException(nameof(resultView));
            if (this.stopRequested || (this.executing && !this.openingBattle) || this.factory != null ||
                (this.State != GameFlowState.Menu && this.State != GameFlowState.LoadingBattle))
                throw new InvalidOperationException($"Cannot attach a battle in {this.State}.");
            if (this.openingBattle) {
                this.factory = factory;
                this.resultView = resultView;
                return;
            }
            this.Execute(() => {
                this.factory = factory;
                this.resultView = resultView;
                this.CreateRun();
            });
        }

        public void Tick(float deltaTime) {
            if (this.executing || this.State != GameFlowState.Battle) return;
            // Keep the per-frame path free of capturing delegates.
            this.executing = true;
            Exception failure = null;
            try {
                this.run.Tick(deltaTime);
                if (this.stopRequested) return;
                var finished = this.run.TryGetResult(out var result);
                if (this.stopRequested || !finished) return;
                // State changes before external callbacks, so they cannot submit a second result.
                this.State = GameFlowState.Results;
                this.progress.Record(result);
                if (this.stopRequested) return;
                var currentProgress = this.progress.Current;
                if (this.stopRequested) return;
                var status = this.progress.Status;
                if (this.stopRequested) return;
                this.resultView.ShowResult(result, currentProgress, status);
            } catch (Exception error) { failure = error; }
            finally { this.EndOperation(failure); }
        }

        public bool Restart() {
            if (this.executing || this.State != GameFlowState.Results) return false;
            return this.Execute(() => {
                this.StopRun();
                if (!this.stopRequested) this.CreateRun();
            });
        }

        public bool ReturnToMenu() {
            if (this.executing || this.State is GameFlowState.Menu or GameFlowState.LoadingBattle or GameFlowState.Disposed) return false;
            return this.Execute(() => {
                this.progress.Flush();
                if (this.stopRequested) return;
                this.StopRun();
                if (this.stopRequested) return;
                this.factory = null;
                this.resultView = null;
                this.State = GameFlowState.Menu;
                this.navigation.OpenMenu();
            });
        }

        // A delayed callback from an older scene cannot dispose a newer scene's run.
        public void DetachBattle(IBattleFactory owner) {
            if (!ReferenceEquals(this.factory, owner) || this.factory == null) return;
            this.Close(GameFlowState.Menu);
        }

        private void CreateRun() {
            this.State = GameFlowState.LoadingBattle;
            this.run = this.factory.Create() ?? throw new InvalidOperationException("Battle factory returned null.");
            // A factory callback may close the scene/controller before returning the run.
            // Retain ownership so EndOperation still releases that returned run.
            if (!this.stopRequested) this.State = GameFlowState.Battle;
        }
        private void StopRun() {
            var current = this.run;
            this.run = null;
            current?.Dispose();
        }
        private bool Execute(Action action) {
            this.executing = true;
            Exception failure = null;
            try { action(); return !this.stopRequested; }
            catch (Exception error) { failure = error; return false; }
            finally { this.EndOperation(failure); }
        }

        private void EndOperation(Exception failure) {
            try {
                if (failure != null && !this.stopRequested) {
                    this.State = GameFlowState.Faulted;
                    this.stopRequested = true;
                }
                if (this.stopRequested) {
                    try { this.StopRun(); }
                    catch (Exception cleanupError) {
                        failure = failure == null ? cleanupError : new AggregateException(failure, cleanupError);
                    }
                }
            } finally {
                this.stopRequested = false;
                this.executing = false;
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void Close(GameFlowState state) {
            this.State = state;
            this.factory = null;
            this.resultView = null;
            this.stopRequested = true;
            // Dispose only after the active external call unwinds, never inside run.Tick.
            if (this.executing) return;
            this.executing = true;
            this.EndOperation(null);
        }
        public void Dispose() {
            if (this.State == GameFlowState.Disposed) return;
            this.Close(GameFlowState.Disposed);
        }
    }
}

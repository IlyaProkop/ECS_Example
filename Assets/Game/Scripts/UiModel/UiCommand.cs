using System;

namespace Game.UiModel {
    public interface IUiCommand {
        bool CanExecute { get; }
        event Action CanExecuteChanged;
        bool TryExecute();
    }

    public sealed class UiCommand : IUiCommand, IDisposable {
        private Func<bool> execute;
        private bool enabled, executing, disposed;
        public bool CanExecute => this.enabled && !this.executing && !this.disposed;
        public event Action CanExecuteChanged;
        public UiCommand(Func<bool> execute, bool enabled = true) {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.enabled = enabled;
        }
        public void SetEnabled(bool value) {
            if (this.disposed || this.enabled == value) return;
            var previous = this.CanExecute;
            this.enabled = value;
            if (previous != this.CanExecute) this.CanExecuteChanged?.Invoke();
        }
        public bool TryExecute() {
            if (!this.CanExecute) return false;
            this.executing = true;
            try {
                this.CanExecuteChanged?.Invoke();
                // A binding callback can close the screen while disabling the button.
                return !this.disposed && this.execute();
            } finally {
                this.executing = false;
                if (!this.disposed) this.CanExecuteChanged?.Invoke();
            }
        }
        public void Dispose() {
            if (this.disposed) return;
            this.disposed = true;
            this.execute = null;
            try { this.CanExecuteChanged?.Invoke(); }
            finally { this.CanExecuteChanged = null; }
        }
    }
}

using System;

namespace Game.UiModel {
    public interface IViewState {
        event Action Changed;
        bool IsDisposed { get; }
    }

    // Main-thread state. A notification publishes a complete update, never half-updated properties.
    public abstract class ViewModel : IViewState, IDisposable {
        public event Action Changed;
        public bool IsDisposed { get; private set; }
        protected void NotifyChanged() { if (!this.IsDisposed) this.Changed?.Invoke(); }
        protected void ThrowIfDisposed() {
            if (this.IsDisposed) throw new ObjectDisposedException(this.GetType().Name);
        }
        public void Dispose() {
            if (this.IsDisposed) return;
            this.IsDisposed = true;
            try { this.DisposeOwned(); }
            finally { this.Changed = null; }
        }
        protected virtual void DisposeOwned() { }
    }
}

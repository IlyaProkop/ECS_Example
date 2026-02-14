using System;
using Game.UiModel;
using UnityEngine;

namespace Game.UI.Binding {
    // The host owns the state. A view owns subscriptions only while enabled.
    public abstract class BoundView<TState> : MonoBehaviour where TState : class, IViewState {
        private ViewBindings bindings;
        protected TState State { get; private set; }
        public void Bind(TState state) {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.IsDisposed) throw new ObjectDisposedException(state.GetType().Name);
            this.Unbind();
            this.State = state;
            if (this.isActiveAndEnabled) this.Activate();
        }
        public void Unbind(TState owner) {
            if (ReferenceEquals(this.State, owner)) this.Unbind();
        }
        public void Unbind() {
            this.Deactivate();
            this.State = null;
        }
        protected abstract void Connect(ViewBindings bindings);
        private void Activate() {
            if (this.State == null || this.State.IsDisposed || this.bindings != null) return;
            this.bindings = new ViewBindings();
            try {
                this.Connect(this.bindings);
                this.bindings.Observe(this.State);
                this.bindings.Refresh();
            } catch { this.Deactivate(); throw; }
        }
        private void Deactivate() {
            var current = this.bindings;
            this.bindings = null;
            current?.Dispose();
        }
        protected virtual void OnEnable() => this.Activate();
        protected virtual void OnDisable() => this.Deactivate();
        protected virtual void OnDestroy() => this.Unbind();
    }
}

using System;
using System.Collections.Generic;
using Game.UiModel;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.UI.Binding {
    // One activation's typed bindings. Delegates/storage are allocated only when connecting a view.
    public sealed class ViewBindings : IDisposable {
        private readonly List<Action> updates = new List<Action>();
        private readonly List<Action> disconnects = new List<Action>();
        private bool disposed;
        public void Property<T>(Func<T> read, Action<T> write) {
            if (this.disposed) throw new ObjectDisposedException(nameof(ViewBindings));
            var initialized = false;
            var previous = default(T);
            this.updates.Add(() => {
                var value = read();
                if (initialized && EqualityComparer<T>.Default.Equals(previous, value)) return;
                previous = value;
                initialized = true;
                write(value);
            });
        }
        public void Text(Text target, Func<string> read) => this.Property(read, value => target.text = value);
        public void Color(Graphic target, Func<Color> read) => this.Property(read, value => target.color = value);
        public void Active(GameObject target, Func<bool> read) => this.Property(read, target.SetActive);
        public void Command(Button target, IUiCommand command) {
            if (this.disposed) throw new ObjectDisposedException(nameof(ViewBindings));
            UnityAction clicked = () => { if (!this.disposed) command.TryExecute(); };
            Action refresh = () => { if (!this.disposed && target != null) target.interactable = command.CanExecute; };
            // Register removal before any callbacks can run.
            this.disconnects.Add(() => {
                command.CanExecuteChanged -= refresh;
                if (target != null) {
                    target.onClick.RemoveListener(clicked);
                    target.interactable = false;
                }
            });
            target.onClick.AddListener(clicked);
            command.CanExecuteChanged += refresh;
            refresh();
        }
        public void Child<T>(BoundView<T> view, T state) where T : class, IViewState {
            if (this.disposed) throw new ObjectDisposedException(nameof(ViewBindings));
            this.disconnects.Add(() => { if (view != null) view.Unbind(state); });
            view.Bind(state);
        }
        public void Observe(IViewState state) {
            if (this.disposed) throw new ObjectDisposedException(nameof(ViewBindings));
            state.Changed += this.Refresh;
            this.disconnects.Add(() => state.Changed -= this.Refresh);
        }
        public void Refresh() {
            // A binding may deactivate its owner; stop immediately if the scope closes.
            for (var i = 0; !this.disposed && i < this.updates.Count; i++) this.updates[i]();
        }
        public void Dispose() {
            if (this.disposed) return;
            this.disposed = true;
            for (var i = this.disconnects.Count - 1; i >= 0; i--) this.disconnects[i]();
            this.disconnects.Clear();
            this.updates.Clear();
        }
    }
}

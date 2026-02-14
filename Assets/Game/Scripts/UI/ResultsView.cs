using Game.UI.Binding;
using Game.UiModel;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI {
    // Attach to the screen host, outside the panel whose visibility is bound.
    public sealed class ResultsView : BoundView<IResultsState> {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text title, details;
        [SerializeField] private Button restart, menu;
        public void Initialize(GameObject panel, Text title, Text details, Button restart, Button menu) {
            this.panel = panel; this.title = title; this.details = details;
            this.restart = restart; this.menu = menu;
        }
        protected override void Connect(ViewBindings bindings) {
            bindings.Text(this.title, () => this.State.Title);
            bindings.Text(this.details, () => this.State.Details);
            bindings.Color(this.title, () => this.State.Victory ? new Color(0.4f, 1f, 0.6f) : new Color(1f, 0.4f, 0.35f));
            bindings.Command(this.restart, this.State.Restart);
            bindings.Command(this.menu, this.State.Menu);
            bindings.Active(this.panel, () => this.State.Visible);
        }
    }
}

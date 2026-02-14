using Game.UI.Binding;
using Game.UiModel;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI {
    public sealed class MenuView : BoundView<IMenuState> {
        [SerializeField] private Text summary, instructions, saveStatus;
        [SerializeField] private Button play;
        public void Initialize(Text summary, Text instructions, Text saveStatus, Button play) {
            this.summary = summary; this.instructions = instructions; this.saveStatus = saveStatus; this.play = play;
        }
        protected override void Connect(ViewBindings bindings) {
            bindings.Text(this.summary, () => this.State.Summary);
            bindings.Text(this.instructions, () => this.State.Instructions);
            bindings.Text(this.saveStatus, () => this.State.SaveStatus);
            bindings.Command(this.play, this.State.Play);
        }
    }
}

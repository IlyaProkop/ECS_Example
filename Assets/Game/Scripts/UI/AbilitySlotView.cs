using Game.UI.Binding;
using Game.UiModel;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI {
    public sealed class AbilitySlotView : BoundView<IAbilitySlotState> {
        private Button button;
        private Text label;
        private Image cooldown;
        public void Initialize(Button button, Text label, Image cooldown) {
            this.button = button; this.label = label; this.cooldown = cooldown;
            this.button.interactable = false;
        }
        protected override void Connect(ViewBindings bindings) {
            bindings.Text(this.label, () => this.State.Label);
            bindings.Property(() => this.State.CooldownFraction, value => this.cooldown.rectTransform.anchorMax = new Vector2(value, 0f));
            bindings.Command(this.button, this.State.Activate);
        }
    }
}

using Game.UI.Binding;
using Game.UiModel;

namespace Game.UI {
    public sealed class AbilityBarView : BoundView<IAbilityBarState> {
        private AbilitySlotView[] slots;
        public void Initialize(AbilitySlotView[] slots) => this.slots = slots;
        protected override void Connect(ViewBindings bindings) {
            for (var i = 0; i < this.slots.Length; i++) {
                var visible = i < this.State.Slots.Count;
                this.slots[i].gameObject.SetActive(visible);
                if (visible) bindings.Child<IAbilitySlotState>(this.slots[i], this.State.Slots[i]);
                else this.slots[i].Unbind();
            }
        }
    }
}

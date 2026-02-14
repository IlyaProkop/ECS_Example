using Game.UI.Binding;
using Game.UiModel;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI {
    public sealed class HudView : BoundView<IHudState> {
        public AbilityBarView Abilities { get; private set; }
        public void SetAbilityBar(AbilityBarView view) => this.Abilities = view;
        [SerializeField] private Text health, coins, objective, controls, statusEffects;
        public void Initialize(Text health, Text coins, Text objective, Text controls, Text statusEffects) {
            this.health = health; this.coins = coins; this.objective = objective;
            this.controls = controls;
            this.statusEffects = statusEffects;
        }
        protected override void Connect(ViewBindings bindings) {
            bindings.Text(this.health, () => this.State.Health);
            bindings.Color(this.health, () => this.State.LowHealth ? new Color(1f, 0.4f, 0.35f) : Color.white);
            bindings.Text(this.coins, () => this.State.Coins);
            bindings.Text(this.objective, () => this.State.Objective);
            bindings.Text(this.controls, () => this.State.Controls);
            bindings.Text(this.statusEffects, () => this.State.StatusEffects);
            bindings.Active(this.statusEffects.gameObject, () => this.State.HasStatusEffects);
        }
    }
}

using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;

namespace Game.ECS.Abilities {
    internal sealed class PlayerAbilityIntentSystem : ISystem {
        private readonly EntityLookup entities;
        private Stash<CombatInputComponent> inputs;
        private Stash<GameStateComponent> states;
        public PlayerAbilityIntentSystem(EntityLookup entities) => this.entities = entities;
        public World World { get; set; }
        public void OnAwake() {
            this.inputs = this.World.GetStash<CombatInputComponent>();
            this.states = this.World.GetStash<GameStateComponent>();
        }
        public void OnUpdate(float dt) {
            if (!GameStateHelper.IsCombat(this.entities, this.states)) return;
            var input = this.inputs.Get(this.entities.player);
            var slots = input.abilitySlotsPressed | (input.abilityPressed ? 1u : 0u);
            for (var i = 0; i < Game.Domain.Abilities.AbilityLoadout.MaxSlots; i++)
                if ((slots & (1u << i)) != 0) AbilityEquipment.Request(this.World, this.entities.player, i);
        }
        public void Dispose() { }
    }
}

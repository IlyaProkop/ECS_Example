using Game.ECS.Components;
using Game.ECS.Core;
using Game.Input;
using Scellecs.Morpeh;

namespace Game.ECS.Systems {
    internal sealed class PlayerInputSystem : ISystem {
        private readonly EntityLookup entities;
        private readonly Game.Input.IPlayerInputReader input;
        private Stash<MoveInputComponent> movement;
        private Stash<CombatInputComponent> combat;
        private Stash<GameStateComponent> states;
        public PlayerInputSystem(EntityLookup entities, Game.Input.IPlayerInputReader input) {
            this.entities = entities;
            this.input = input;
        }
        public World World { get; set; }
        public void OnAwake() {
            this.movement = this.World.GetStash<MoveInputComponent>();
            this.combat = this.World.GetStash<CombatInputComponent>();
            this.states = this.World.GetStash<GameStateComponent>();
        }
        public void OnUpdate(float deltaTime) {
            if (!this.entities.TryGetPlayer(out var player)) return;
            var frame = GameStateHelper.IsGameOver(this.entities, this.states) ? default : this.input.ReadFrame();
            this.movement.Get(player).value = frame.move;
            this.combat.Set(player, new CombatInputComponent {
                attackHeld = frame.attackHeld, abilityPressed = frame.abilityPressed, abilitySlotsPressed = frame.abilitySlotsPressed, aim = frame.aim
            });
        }
        public void Dispose() { }
    }
}

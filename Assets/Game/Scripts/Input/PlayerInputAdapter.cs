using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Input {
    public sealed class PlayerInputAdapter : MonoBehaviour, IPlayerInputReader {
        private IMoveInputSource source;

        private void Awake() {
            this.source = new KeyboardMoveInputSource();
        }

        public PlayerInputFrame ReadFrame() {
            var keyboard = Keyboard.current;
            return new PlayerInputFrame {
                move = this.source.ReadMoveVector(),
                attackHeld = ((Mouse.current?.leftButton.isPressed ?? false)
                    && !(UnityEngine.EventSystems.EventSystem.current?.IsPointerOverGameObject() ?? false)) || (keyboard?.jKey.isPressed ?? false),
                abilitySlotsPressed = ((keyboard?.digit1Key.wasPressedThisFrame ?? false) ? 1u : 0u)
                    | ((keyboard?.digit2Key.wasPressedThisFrame ?? false) ? 2u : 0u)
                    | ((keyboard?.digit3Key.wasPressedThisFrame ?? false) ? 4u : 0u)
                    | ((keyboard?.digit4Key.wasPressedThisFrame ?? false) ? 8u : 0u),
                abilityPressed = (keyboard?.spaceKey.wasPressedThisFrame ?? false) || (keyboard?.eKey.wasPressedThisFrame ?? false)
            };
        }
    }
}

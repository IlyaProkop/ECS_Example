using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.Input {
    public sealed class KeyboardMoveInputSource : IMoveInputSource {
        public Vector2 ReadMoveVector() {
            var x = 0f;
            var y = 0f;

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) {
                return Vector2.zero;
            }

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) {
                x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) {
                x += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) {
                y -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) {
                y += 1f;
            }
#else
            throw new System.InvalidOperationException("Game requires Unity Input System to be enabled.");
#endif

            var vector = new Vector2(x, y);
            return vector.sqrMagnitude > 1f ? vector.normalized : vector;
        }
    }
}

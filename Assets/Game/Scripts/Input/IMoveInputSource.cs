using UnityEngine;

namespace Game.Input {
    public interface IMoveInputSource {
        Vector2 ReadMoveVector();
    }
}

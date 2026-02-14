using UnityEngine;

namespace Game.Input {
    public struct PlayerInputFrame {
        public Vector2 move;
        public bool attackHeld;
        public bool abilityPressed;
        public uint abilitySlotsPressed;
        public Game.Domain.AttackAim aim;
    }

    public interface IPlayerInputReader {
        PlayerInputFrame ReadFrame();
    }
}

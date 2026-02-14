using System;
using Game.Domain;
using Game.Input;
using UnityEngine;

namespace Game.ECS.Core {
    internal sealed class SimulationInput : IPlayerInputReader {
        public PlayerInputFrame Frame;
        public PlayerInputFrame ReadFrame() => this.Frame;

        // Continuous values use the latest host frame; an edge waits for a completed step.
        public void Submit(in PlayerInputFrame input) {
            if (float.IsNaN(input.move.x) || float.IsInfinity(input.move.x) ||
                float.IsNaN(input.move.y) || float.IsInfinity(input.move.y) ||
                float.IsNaN(input.aim.direction.x) || float.IsInfinity(input.aim.direction.x) ||
                float.IsNaN(input.aim.direction.y) || float.IsInfinity(input.aim.direction.y) ||
                input.aim.kind < AttackAimKind.NearestHostile || input.aim.kind > AttackAimKind.Direction)
                throw new ArgumentException("Input must be finite and use a supported aim kind.", nameof(input));
            this.Frame.move = Vector2.ClampMagnitude(input.move, 1f);
            this.Frame.attackHeld = input.attackHeld;
            this.Frame.aim = input.aim;
            this.Frame.abilityPressed |= input.abilityPressed;
            this.Frame.abilitySlotsPressed |= input.abilitySlotsPressed & ((1u << Game.Domain.Abilities.AbilityLoadout.MaxSlots) - 1u);
        }

        public void CompleteStep() { this.Frame.abilityPressed = false; this.Frame.abilitySlotsPressed = 0; }
    }
}

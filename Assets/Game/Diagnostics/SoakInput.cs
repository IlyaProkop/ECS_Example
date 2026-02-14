using System;
using System.Collections.Generic;
using Game.Domain;
using Game.ECS.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Game.Diagnostics {
    // Only this diagnostic process uses virtual devices. Events still pass through the
    // real PlayerInputAdapter. Other applications' physical input is unaffected.
    internal sealed class SoakInput : IDisposable {
        private readonly Func<GameSession> readSession;
        private readonly List<InputDevice> suspended = new List<InputDevice>();
        private readonly Keyboard keyboard;
        private readonly Mouse mouse;
        private readonly InputSettings.BackgroundBehavior background;
        private readonly Vector3[] route = { new Vector3(11.5f, 0, -9.25f), new Vector3(11.5f, 0, 9.25f),
            new Vector3(-11.5f, 0, 9.25f), new Vector3(-11.5f, 0, -9.25f) };
        private int waypoint;
        private double nextAbility;
        private bool abilityReleased = true;
        public bool Attack { get; private set; }

        public SoakInput(Func<GameSession> readSession) {
            this.readSession = readSession;
            this.background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (var device in InputSystem.devices)
                if (device.enabled && (device is Keyboard || device is Mouse)) this.suspended.Add(device);
            foreach (var device in this.suspended) InputSystem.DisableDevice(device);
            this.keyboard = InputSystem.AddDevice<Keyboard>();
            this.mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.onBeforeUpdate += this.BeforeInput;
        }
        public void BeginBattle(bool attack) {
            this.Attack = attack;
            this.waypoint = 0;
            this.nextAbility = Time.realtimeSinceStartupAsDouble;
            this.abilityReleased = true;
        }
        private void BeforeInput() {
            if (InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            var state = new KeyboardState();
            var session = this.readSession();
            if (session != null && !session.Snapshot.isGameOver) {
                if (session.Snapshot.phase == SessionPhase.AwaitingEntry) state.Set(Key.W, true);
                else if (this.Attack) {
                    var delta = this.route[this.waypoint] - session.PlayerPosition;
                    if (delta.sqrMagnitude < 0.2f) {
                        this.waypoint = (this.waypoint + 1) % this.route.Length;
                        delta = this.route[this.waypoint] - session.PlayerPosition;
                    }
                    var move = delta.normalized;
                    state.Set(Key.D, move.x > 0.15f); state.Set(Key.A, move.x < -0.15f);
                    state.Set(Key.W, move.z > 0.15f); state.Set(Key.S, move.z < -0.15f);
                }
                state.Set(Key.J, this.Attack);
                var ability = this.Attack && this.abilityReleased && Time.realtimeSinceStartupAsDouble >= this.nextAbility;
                state.Set(Key.Space, ability);
                this.abilityReleased = !ability;
                if (ability) this.nextAbility = Time.realtimeSinceStartupAsDouble + 5.1;
            }
            InputSystem.QueueStateEvent(this.keyboard, state);
        }
        public void Dispose() {
            InputSystem.onBeforeUpdate -= this.BeforeInput;
            if (this.keyboard != null && this.keyboard.added) InputSystem.RemoveDevice(this.keyboard);
            if (this.mouse != null && this.mouse.added) InputSystem.RemoveDevice(this.mouse);
            foreach (var device in this.suspended) if (device.added) InputSystem.EnableDevice(device);
            InputSystem.settings.backgroundBehavior = this.background;
        }
    }
}

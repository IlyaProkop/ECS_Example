using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Game.UI {
    // Scene ownership of actions and reference assets. The input module borrows them
    // and only enables/disables them; re-enabling the scene reuses the same actions.
    internal sealed class SceneUiInputScope : MonoBehaviour {
        private InputSystemUIInputModule module;
        private DefaultInputActions actions;
        private readonly InputActionReference[] references = new InputActionReference[10];
        private int referenceCount;

        public static void Attach(GameObject target) {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var wasActive = target.activeSelf;
            // Input System 1.18.0 auto-assignment inside OnEnable registers actions twice.
            // Configure the module before OnEnable, without touching package internals.
            target.SetActive(false);
            SceneUiInputScope scope = null;
            try {
                scope = target.AddComponent<SceneUiInputScope>();
                scope.Initialize(target.AddComponent<InputSystemUIInputModule>());
            } catch {
                if (scope != null) { scope.Release(); ReleaseObject(scope); }
                throw;
            } finally { target.SetActive(wasActive); }
        }

        private void Initialize(InputSystemUIInputModule inputModule) {
            this.module = inputModule;
            this.actions = new DefaultInputActions();
            this.module.actionsAsset = this.actions.asset;
            var ui = this.actions.UI;
            this.module.point = this.Reference(ui.Point);
            this.module.leftClick = this.Reference(ui.Click);
            this.module.rightClick = this.Reference(ui.RightClick);
            this.module.middleClick = this.Reference(ui.MiddleClick);
            this.module.scrollWheel = this.Reference(ui.ScrollWheel);
            this.module.move = this.Reference(ui.Navigate);
            this.module.submit = this.Reference(ui.Submit);
            this.module.cancel = this.Reference(ui.Cancel);
            this.module.trackedDevicePosition = this.Reference(ui.TrackedDevicePosition);
            this.module.trackedDeviceOrientation = this.Reference(ui.TrackedDeviceOrientation);
        }
        private InputActionReference Reference(InputAction action) {
            var reference = InputActionReference.Create(action);
            this.references[this.referenceCount++] = reference;
            return reference;
        }
        private void OnDestroy() => this.Release();
        private void Release() {
            if (this.module != null) {
                this.module.enabled = false; // Unhook and balance action references before asset destruction.
                this.module.actionsAsset = null;
                ReleaseObject(this.module);
                this.module = null;
            }
            for (var i = 0; i < this.referenceCount; i++) {
                ReleaseObject(this.references[i]);
                this.references[i] = null;
            }
            this.referenceCount = 0;
            if (this.actions != null) { ReleaseObject(this.actions.asset); this.actions = null; }
        }
        private static void ReleaseObject(UnityEngine.Object value) {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}

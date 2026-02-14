using Game.Config;
using Game.UI;
using Game.UI.Layout;
using Game.UiModel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.SceneFlow {
    // Menu composition/lifetime only. Values and commands belong to MenuViewModel.
    public sealed class MenuSceneController : MonoBehaviour {
        private MenuView view;
        private MenuViewModel model;
        private void OnEnable() {
            if (!SceneNames.IsMenuScene(SceneManager.GetActiveScene())) return;
            this.EnsureCamera();
            UiElements.EnsureEventSystem(this.transform);
            if (this.view == null) this.view = MenuUiBuilder.Create(this.transform);
            var progress = ApplicationServices.Progress;
            progress.Flush();
            var config = Resources.Load<GameConfig>("GameConfig");
            this.model = new MenuViewModel(progress, ApplicationServices.Flow,
                config != null ? config.AbilityDisplayName : AbilityAsset.DefaultDisplayName);
            try { this.model.Refresh(); this.view.Bind(this.model); }
            catch { this.ReleaseModel(); throw; }
        }
        private void OnDisable() => this.ReleaseModel();
        private void OnDestroy() => this.ReleaseModel();
        private void ReleaseModel() {
            if (this.model == null) return;
            var current = this.model;
            this.model = null;
            try { if (this.view != null) this.view.Unbind(current); }
            finally { current.Dispose(); }
        }
        private void EnsureCamera() {
            var cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            if (cameras.Length == 0) {
                var cameraObject = new GameObject("MenuCamera");
                cameraObject.transform.SetParent(this.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                cameraObject.AddComponent<AudioListener>();
                return;
            }

            for (var i = 0; i < cameras.Length; i++) {
                cameras[i].enabled = i == 0;
                if (i == 0) {
                    cameras[i].clearFlags = CameraClearFlags.SolidColor;
                    cameras[i].backgroundColor = new Color(0.08f, 0.1f, 0.14f);
                }
            }
        }

    }
}

using UnityEngine;
using UnityEngine.SceneManagement;
using Game.SceneFlow;

namespace Game {
    public static class AutoBootstrap {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize() {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            var activeScene = SceneManager.GetActiveScene();
            if (StartupSceneRouter.EnsureBootstrapEntry(activeScene)) {
                return;
            }

            HandleScene(activeScene);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            if (StartupSceneRouter.EnsureBootstrapEntry(scene)) {
                return;
            }

            HandleScene(scene);
        }

        private static void HandleScene(Scene scene) {
            if (SceneNames.IsBootstrapScene(scene)) {
                EnsureObject<BootstrapSceneController>("BootstrapSceneController");
                return;
            }

            if (SceneNames.IsMenuScene(scene)) {
                EnsureObject<MenuSceneController>("MenuSceneController");
                return;
            }

            if (SceneNames.IsGameScene(scene)) {
                EnsureObject<GameBootstrap>("GameBootstrap");
                return;
            }
        }

        private static void EnsureObject<T>(string objectName) where T : Component {
            var existing = Object.FindFirstObjectByType<T>();
            if (existing != null) {
                return;
            }

            var bootstrapObject = new GameObject(objectName);
            bootstrapObject.AddComponent<T>();
        }
    }
}

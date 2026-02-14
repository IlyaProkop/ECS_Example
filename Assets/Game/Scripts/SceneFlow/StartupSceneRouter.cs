using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.SceneFlow {
    public static class StartupSceneRouter {
        private static bool startupSceneChecked;
        private static string pendingInitialScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() {
            startupSceneChecked = false;
            pendingInitialScene = null;
        }

        public static bool EnsureBootstrapEntry(Scene activeScene) {
            if (startupSceneChecked) {
                return false;
            }

            startupSceneChecked = true;
            var activeSceneName = activeScene.name;

            if (string.IsNullOrWhiteSpace(activeSceneName) || !SceneNames.IsKnownSceneName(activeSceneName)) {
                return false;
            }

            if (SceneNames.IsBootstrapSceneName(activeSceneName)) {
                return false;
            }

            pendingInitialScene = activeSceneName;
            SceneManager.LoadScene(SceneNames.Bootstrap, LoadSceneMode.Single);
            return true;
        }

        public static bool TryConsumePendingInitialScene(out string sceneName) {
            if (string.IsNullOrWhiteSpace(pendingInitialScene)) {
                sceneName = null;
                return false;
            }

            sceneName = pendingInitialScene;
            pendingInitialScene = null;
            return true;
        }
    }
}

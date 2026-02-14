using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.SceneFlow {
    public sealed class BootstrapSceneController : MonoBehaviour {
        [SerializeField] private bool autoLoadMenuScene = true;

        private void Start() {
            var activeScene = SceneManager.GetActiveScene();

            if (!SceneNames.IsBootstrapScene(activeScene)) {
                return;
            }

            if (StartupSceneRouter.TryConsumePendingInitialScene(out var pendingInitialScene) &&
                !SceneNames.IsBootstrapSceneName(pendingInitialScene)) {
                SceneManager.LoadScene(pendingInitialScene, LoadSceneMode.Single);
                return;
            }

            if (this.autoLoadMenuScene) {
                SceneManager.LoadScene(SceneNames.Menu, LoadSceneMode.Single);
            }
        }
    }
}

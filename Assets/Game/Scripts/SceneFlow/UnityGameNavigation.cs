using Game.Flow;
using UnityEngine.SceneManagement;

namespace Game.SceneFlow {
    public sealed class UnityGameNavigation : IGameNavigation {
        public void OpenBattle() => SceneManager.LoadSceneAsync(SceneNames.Game, LoadSceneMode.Single);
        public void OpenMenu() => SceneManager.LoadSceneAsync(SceneNames.Menu, LoadSceneMode.Single);
    }
}

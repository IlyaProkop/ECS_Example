using System;
using UnityEngine.SceneManagement;

namespace Game.SceneFlow {
    public static class SceneNames {
        public const string Bootstrap = "Bootstrap";
        public const string Menu = "Menu";
        public const string Game = "Game";

        public const string BootstrapPath = "Assets/Scenes/Bootstrap.unity";
        public const string MenuPath = "Assets/Scenes/Menu.unity";
        public const string GamePath = "Assets/Scenes/Game.unity";

        public static bool IsBootstrapScene(Scene scene) {
            return IsBootstrapSceneName(scene.name);
        }

        public static bool IsMenuScene(Scene scene) {
            return IsMenuSceneName(scene.name);
        }

        public static bool IsGameScene(Scene scene) {
            return IsGameSceneName(scene.name);
        }

        public static bool IsBootstrapSceneName(string sceneName) {
            return string.Equals(sceneName, Bootstrap, StringComparison.Ordinal);
        }

        public static bool IsMenuSceneName(string sceneName) {
            return string.Equals(sceneName, Menu, StringComparison.Ordinal);
        }

        public static bool IsGameSceneName(string sceneName) {
            return string.Equals(sceneName, Game, StringComparison.Ordinal);
        }

        public static bool IsKnownSceneName(string sceneName) {
            return IsBootstrapSceneName(sceneName) || IsMenuSceneName(sceneName) || IsGameSceneName(sceneName);
        }
    }
}

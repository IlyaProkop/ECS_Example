using System;
using System.IO;
using Game.Config;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Game.Editor {
    public static class BuildCommands {
        [MenuItem("Game/Build Windows prototype")]
        public static void BuildWindows() => BuildWindows(BuildOptions.None);

        [MenuItem("Game/Build Windows profiling player")]
        public static void BuildWindowsProfile() => BuildWindows(BuildOptions.Development);

        private static void BuildWindows(BuildOptions options) {
            ValidateBuildContent();
            var output = GetOutputPath((options & BuildOptions.Development) != 0);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Scenes/Bootstrap.unity", "Assets/Scenes/Menu.unity", "Assets/Scenes/Game.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = options
            });
            if (report.summary.result != BuildResult.Succeeded) {
                throw new InvalidOperationException($"Build failed: {report.summary.result}, {report.summary.totalErrors} errors");
            }
            Debug.Log($"Windows build ready: {output} ({report.summary.totalSize} bytes)");
        }

        private static string GetOutputPath(bool profiling) {
            var arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length; i++) {
                if (arguments[i] != "-arena-build-output") continue;
                if (i + 1 == arguments.Length || !arguments[i + 1].EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("-arena-build-output requires an executable path ending in .exe.");
                return Path.GetFullPath(arguments[i + 1]);
            }
            return Path.GetFullPath(profiling ? "Builds/ProfileWindows/Arena.exe" : "Builds/Windows/Arena.exe");
        }

        private static void ValidateBuildContent() {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Game/Resources/GameConfig.asset");
            if (config == null || config.playerAbility == null ||
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Resources/ArenaMaterial.mat") == null)
                throw new InvalidOperationException("Build content is missing. Restore the authored config, ability and material before building.");
            // Validate authored definitions and their runtime registrations without changing assets.
            _ = new Game.Rendering.ActorVisualCatalog(config);
            using var session = new Game.ECS.Core.GameSession(config.CreateSimulationSettings());
        }

        [MenuItem("Game/Select balance config")]
        public static void EnsureConfig() {
            const string directory = "Assets/Game/Resources";
            const string path = directory + "/GameConfig.asset";
            if (!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder("Assets/Game", "Resources");
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
            if (config == null) {
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
            }
            const string abilityPath = directory + "/Shockwave.asset";
            if (config.playerAbility == null) {
                var ability = AssetDatabase.LoadAssetAtPath<AbilityAsset>(abilityPath);
                if (ability == null) {
                    ability = ScriptableObject.CreateInstance<AbilityAsset>();
                    AssetDatabase.CreateAsset(ability, abilityPath);
                }
                config.playerAbility = ability;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = config;
            const string materialPath = directory + "/ArenaMaterial.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null) {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
                AssetDatabase.CreateAsset(material, materialPath);
                AssetDatabase.SaveAssets();
            }
        }
    }
}

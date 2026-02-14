using Game.Config;
using Game.ECS.Core;
using Game.Flow;
using Game.SceneFlow;
using UnityEngine;
using Unity.Profiling;
using System.Collections.Generic;

namespace Game {
    // Unity lifecycle adapter. Application policy is owned by GameFlowController.
    public sealed class GameBootstrap : MonoBehaviour {
        private static readonly ProfilerMarker CreateMarker = new ProfilerMarker("Arena.Startup.Bootstrap");
        [SerializeField] private GameConfig config;
        private GameConfig runtimeConfig;
        private ArenaSceneBuilder scene;
        private ArenaBattleFactory battles;
        private GameFlowController flow;
        private IEnumerator<object> preparation;
        public GameSession Session => this.battles?.Session;

        private void OnEnable() {
            if (!Application.isPlaying) return;
            using var marker = CreateMarker.Auto();
            this.BuildScene();
            this.scene.ShowPreparation(true);
            this.preparation = GameSession.CreateSteps(this.runtimeConfig.CreateSimulationSettings(), this.AttachSession, captureRenderFrames: true);
        }
        private void BuildScene() {
            if (this.scene != null) return;
            var source = this.config != null ? this.config : Resources.Load<GameConfig>("GameConfig");
            this.runtimeConfig = source != null ? Instantiate(source) : ScriptableObject.CreateInstance<GameConfig>();
            this.flow = ApplicationServices.Flow;
            this.scene = new ArenaSceneBuilder(this.transform, this.runtimeConfig);
        }
        private void AttachSession(GameSession session) {
            this.battles = new ArenaBattleFactory(this.runtimeConfig, this.scene, this.flow, preparedSession: session);
            this.scene.ShowPreparation(false);
            this.flow.AttachBattle(this.battles, this.battles);
        }
        private void Update() {
            if (this.preparation != null) {
                var until = Time.realtimeSinceStartupAsDouble + .004;
                try {
                    do {
                        if (this.preparation.MoveNext()) continue;
                        this.CancelPreparation();
                        break;
                    } while (Time.realtimeSinceStartupAsDouble < until);
                } catch { this.CancelPreparation(); throw; }
                // Loading-frame delta must not be replayed as gameplay catch-up.
                return;
            }
            this.flow.Tick(Time.deltaTime);
        }
        private void CancelPreparation() {
            var pending = this.preparation;
            this.preparation = null;
            pending?.Dispose();
        }
        private void OnDisable() {
            try { this.CancelPreparation(); }
            finally {
                try { this.flow?.DetachBattle(this.battles); }
                finally { this.battles?.ReleasePreparedSession(); }
            }
        }
        private void OnDestroy() {
            this.flow?.DetachBattle(this.battles);
            this.scene?.Dispose();
            if (this.runtimeConfig != null) Destroy(this.runtimeConfig);
        }
        private void OnApplicationQuit() => ApplicationServices.Progress.Flush();
    }
}

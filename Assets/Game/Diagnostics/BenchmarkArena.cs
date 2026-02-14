using System;
using Game.Config;
using Game.Domain;
using Game.ECS.Core;
using Game.Flow;
using Game.Input;
using Game.SceneFlow;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Diagnostics {
    internal sealed class BenchmarkArena : IDisposable {
        private readonly GameObject root;
        private readonly GameConfig config;
        private readonly ArenaBattleFactory factory;
        private readonly BenchmarkScenario scenario;
        public int SettleFrames => this.scenario.SettleFrames;
        private readonly RouteInput input = new RouteInput();
        public readonly ArenaSceneBuilder Scene;
        public readonly GameFlowController Flow;
        public GameSession Session => this.factory.Session;
        public Vector2 ArenaHalfSize => this.config.arenaHalfSize;
        public BenchmarkArena(int count, bool durable, int labelLimit, string scenario = "route") {
            this.root = new GameObject("MeasuredArena");
            this.config = Object.Instantiate(Resources.Load<GameConfig>("GameConfig"));
            this.config.enemyCount = count; this.config.enemySpawnInterval = GameSession.FixedStep;
            // A 512-actor mixed population cannot fit without overlap in the authored arena.
            // Enlarge only the stress case; retain the real finite-wave placement rules.
            if (count >= 512) this.config.arenaHalfSize *= 2f * Mathf.Sqrt(count / 512f);
            this.config.coinDropChance = 0f;
            if (labelLimit >= 0) this.config.enemyWorldHpMaxLabels = labelLimit;
            if (durable) {
                this.config.playerMaxHealth = 1000000f;
                foreach (var type in this.config.enemyTypes) type.maxHealth = 1000000f;
            }
            try {
                this.scenario = new BenchmarkScenario(scenario);
                this.scenario.Configure(this.config);
                this.input.Stationary = this.scenario.Stationary;
                this.input.Target = this.scenario.Target;
                this.Scene = new ArenaSceneBuilder(this.root.transform, this.config);
                this.Flow = new GameFlowController(new MemoryProgress(), new NoNavigation());
                this.factory = new ArenaBattleFactory(this.config, this.Scene, this.Flow, this.input);
                this.Flow.AttachBattle(this.factory, this.factory);
            } catch { this.Dispose(); throw; }
        }
        public void Tick() {
            this.input.Position = this.Session.PlayerPosition;
            this.input.Entering = this.Session.Snapshot.phase == SessionPhase.AwaitingEntry;
            this.Flow.Tick(GameSession.FixedStep);
        }
        public void FinishBattle() {
            for (var i = 0; i < 6000 && this.Session.Snapshot.phase != SessionPhase.Meta; i++) {
                var entering = this.Session.Snapshot.phase == SessionPhase.AwaitingEntry;
                this.Session.TickUpdate(GameSession.FixedStep, new PlayerInputFrame { move = entering ? Vector2.up : Vector2.zero });
            }
            this.Flow.Tick(0f);
            if (this.Flow.State != GameFlowState.Results) throw new InvalidOperationException("Lifecycle battle did not reach results.");
        }
        public void Dispose() {
            try { this.Flow?.Dispose(); }
            finally { this.Scene?.Dispose(); this.scenario?.Dispose(); Object.Destroy(this.root); Object.Destroy(this.config); }
        }
        private sealed class RouteInput : IPlayerInputReader {
            public Vector3 Position;
            public bool Entering, Stationary;
            public Vector3 Target;
            private int waypoint, frame;
            private readonly Vector3[] route = { new Vector3(11.5f,0,-9.25f), new Vector3(11.5f,0,9.25f),
                new Vector3(-11.5f,0,9.25f), new Vector3(-11.5f,0,-9.25f) };
            public PlayerInputFrame ReadFrame() {
                var delta = (this.Stationary ? this.Target : this.route[this.waypoint]) - this.Position;
                if (!this.Stationary && delta.sqrMagnitude < 0.2f) { this.waypoint = (this.waypoint + 1) % this.route.Length; delta = this.route[this.waypoint] - this.Position; }
                var direction = this.Stationary && delta.sqrMagnitude < 0.04f ? Vector3.zero : delta.normalized;
                return new PlayerInputFrame { move = this.Entering ? Vector2.up : new Vector2(direction.x, direction.z),
                    attackHeld = true, abilityPressed = !this.Stationary && this.frame++ % 301 == 0 };
            }
        }
        private sealed class NoNavigation : IGameNavigation { public void OpenBattle() { } public void OpenMenu() { } }
        private sealed class MemoryProgress : IProgressService {
            public PlayerProgress Current { get; } = new PlayerProgress();
            public string Status => string.Empty;
            public void Record(in BattleResult result) { }
            public void Flush() { }
        }
    }
}

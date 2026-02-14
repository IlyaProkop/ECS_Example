using System;
using Game.Config;
using Game.Domain;
using Game.Domain.Abilities;
using Game.ECS.Core;
using Game.Flow;
using Game.Rendering;
using Game.UI;
using Game.Input;
using Unity.Profiling;

namespace Game.SceneFlow {
    internal sealed class ArenaBattleFactory : IBattleFactory, IResultView {
        private static readonly ProfilerMarker CreateMarker = new ProfilerMarker("Arena.Startup.Battle");
        private static readonly ProfilerMarker SessionMarker = new ProfilerMarker("Arena.Startup.Session");
        private static readonly ProfilerMarker PresenterMarker = new ProfilerMarker("Arena.Startup.Presenter");
        private static readonly ProfilerMarker UiMarker = new ProfilerMarker("Arena.Startup.BindUI");
        private readonly GameConfig config;
        private readonly ArenaSceneBuilder scene;
        private readonly IGameCommands commands;
        private readonly IPlayerInputReader input;
        private BattleUiSession currentUi;
        private GameSession preparedSession;
        public GameSession Session { get; private set; }
        public ArenaBattleFactory(GameConfig config, ArenaSceneBuilder scene, IGameCommands commands, IPlayerInputReader input = null,
            GameSession preparedSession = null) {
            this.config = config; this.scene = scene; this.commands = commands;
            this.input = input ?? scene.Input;
            this.preparedSession = preparedSession;
        }
        public IBattleRun Create() {
            using var marker = CreateMarker.Auto();
            if (this.Session != null) throw new InvalidOperationException("Dispose the current battle before creating another one.");
            this.scene.ResetSession();
            var session = this.preparedSession;
            this.preparedSession = null;
            if (session == null) {
                using (SessionMarker.Auto()) session = new GameSession(this.config.CreateSimulationSettings(), captureRenderFrames: true);
            }
            ArenaPresenter presenter = null;
            BattleUiSession ui = null;
            try {
                using (PresenterMarker.Auto()) presenter = new ArenaPresenter(this.config, this.scene.Camera, this.scene.RuntimeRoot,
                    this.scene.EnemyHealthUi, session.ViewCapacity, session.AbilityEventCapacity);
                using (UiMarker.Auto()) ui = new BattleUiSession(this.scene.Hud, this.scene.Results, this.commands,
                    this.config.AbilityDisplayName, session.Snapshot, this.config.AbilityDisplayNames(), session);
                var run = new ArenaBattleRun(session, presenter, ui, this.scene, this.input, () => {
                    this.Session = null; this.currentUi = null;
                });
                this.Session = session;
                this.currentUi = ui;
                return run;
            } catch {
                try { ui?.Dispose(); }
                finally { try { presenter?.Dispose(); } finally { session.Dispose(); } }
                throw;
            }
        }
        public void ShowResult(in BattleResult result, PlayerProgress progress, string saveStatus) =>
            this.currentUi.ShowResult(result, progress, saveStatus);
        internal void ReleasePreparedSession() {
            var pending = this.preparedSession;
            this.preparedSession = null;
            pending?.Dispose();
        }

        private sealed class ArenaBattleRun : IBattleRun {
            private static readonly ProfilerMarker TickMarker = new ProfilerMarker("Arena.Battle.Tick");
            private static readonly ProfilerMarker UiMarker = new ProfilerMarker("Arena.UI.Update");
            private readonly GameSession session;
            private readonly ArenaPresenter presenter;
            private readonly BattleUiSession ui;
            private readonly ArenaSceneBuilder scene;
            private readonly Action onDisposed;
            private readonly IPlayerInputReader input;
            private readonly ActorView[] actors;
            private readonly AbilityCastEvent[] events;
            private bool disposed;
            public ArenaBattleRun(GameSession session, ArenaPresenter presenter, BattleUiSession ui, ArenaSceneBuilder scene, IPlayerInputReader input, Action onDisposed) {
                this.session = session; this.presenter = presenter; this.ui = ui; this.scene = scene; this.onDisposed = onDisposed;
                this.input = input;
                this.actors = new ActorView[session.ViewCapacity];
                this.events = new AbilityCastEvent[session.AbilityEventCapacity];
            }
            public void Tick(float deltaTime) {
                using var tick = TickMarker.Auto();
                this.session.TickUpdate(deltaTime, this.input.ReadFrame());
                using (UiMarker.Auto()) this.ui.Update(this.session.Snapshot);
                var actorCount = this.session.CopyRenderActors(this.actors);
                var eventCount = this.session.CopyAbilityEvents(this.events);
                this.presenter.Render(this.actors.AsSpan(0, actorCount), this.session.Snapshot, this.events.AsSpan(0, eventCount), deltaTime);
                this.scene.RenderPhase(this.session.Snapshot.phase);
            }
            public bool TryGetResult(out BattleResult result) => this.session.TryGetResult(out result);
            public void Dispose() {
                if (this.disposed) return;
                this.disposed = true;
                try { this.ui.Dispose(); }
                finally {
                    try { this.presenter.Dispose(); }
                    finally {
                        try { this.session.Dispose(); }
                        finally { this.onDisposed(); }
                    }
                }
            }
        }
    }
}

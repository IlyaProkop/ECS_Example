using System;
using System.Collections.Generic;
using Game.Domain;
using Scellecs.Morpeh;
using Unity.Profiling;

namespace Game.ECS.Core {
    // Owns partial resources until a fully initialized GameSession accepts them.
    // The same ordered steps serve immediate creation and frame-spread startup.
    internal sealed class SessionConstruction : IDisposable {
        private static readonly ProfilerMarker ContextMarker = new ProfilerMarker("Arena.Startup.Context");
        private static readonly ProfilerMarker FactoryMarker = new ProfilerMarker("Arena.Startup.FactoryStep");
        private static readonly ProfilerMarker EntitiesMarker = new ProfilerMarker("Arena.Startup.Entities");
        private static readonly ProfilerMarker InstallMarker = new ProfilerMarker("Arena.Startup.Install");
        private static readonly ProfilerMarker AwakeMarker = new ProfilerMarker("Arena.Startup.FirstUpdate");
        private IEnumerator<object> steps;
        private bool complete, transferred, disposed;
        internal GameContext Context { get; private set; }
        internal World World { get; private set; }
        internal SessionReader Reader { get; private set; }
        internal SessionRenderFrames RenderFrames { get; private set; }
        public SessionConstruction(SimulationSettings config, DamagePipeline damagePipeline, bool captureRenderFrames) {
            if (config == null) throw new ArgumentNullException(nameof(config));
            this.steps = this.Initialize(config, damagePipeline, captureRenderFrames);
        }
        public bool MoveNext() {
            if (this.disposed) throw new ObjectDisposedException(nameof(SessionConstruction));
            if (this.complete) return false;
            try { this.complete = !this.steps.MoveNext(); return !this.complete; }
            catch { this.Dispose(); throw; }
        }
        public void Transfer() {
            if (!this.complete || this.disposed || this.transferred) throw new InvalidOperationException("Complete construction before transferring ownership.");
            this.transferred = true;
        }
        public static SessionConstruction Complete(SimulationSettings config, DamagePipeline damagePipeline, bool capture) {
            var creation = new SessionConstruction(config, damagePipeline, capture);
            while (creation.MoveNext()) { }
            return creation;
        }
        private IEnumerator<object> Initialize(SimulationSettings config, DamagePipeline damagePipeline, bool capture) {
            using (ContextMarker.Auto()) this.Context = new GameContext(config, damagePipeline);
            yield return null;
            this.World = Scellecs.Morpeh.World.Create("Arena Simulation");
            this.World.UpdateByUnity = false;
            yield return null;
            EntityFactory factory = null;
            using (var factorySteps = EntityFactory.CreateSteps(this.World, config, this.Context.NavigationRandom, this.Context.Stats, this.Context.Projectiles, this.Context.Weapons, this.Context.ProjectilePool, value => factory = value)) {
                while (true) {
                    bool more;
                    using (FactoryMarker.Auto()) more = factorySteps.MoveNext();
                    if (!more) break;
                    yield return null;
                }
            }
            using (EntitiesMarker.Auto()) {
                this.Context.EntityLookup.gameState = factory.CreateGameStateEntity();
                this.Context.EntityLookup.player = factory.Players.Spawn(config.PlayerSpawn);
            }
            yield return null;
            using (InstallMarker.Auto()) new GameplayInstaller(this.Context, factory).Install(this.World);
            yield return null;
            this.Reader = new SessionReader(this.World, this.Context.EntityLookup, config.enemyCount, this.Context.Stats);
            if (capture) {
                this.RenderFrames = new SessionRenderFrames(config.ViewCapacity);
            }
            yield return null;
            using (AwakeMarker.Auto()) this.World.Update(0f);
            yield return null;
        }
        public void Dispose() {
            if (this.disposed) return;
            this.disposed = true;
            var pending = this.steps;
            this.steps = null;
            try { pending.Dispose(); }
            finally {
                if (!this.transferred) {
                    try { this.World?.Dispose(); }
                    finally { this.Context?.Dispose(); }
                }
                this.World = null; this.Context = null; this.Reader = null; this.RenderFrames = null;
            }
        }
    }
}

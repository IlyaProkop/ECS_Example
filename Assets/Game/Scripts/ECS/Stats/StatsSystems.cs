using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;

namespace Game.ECS.Stats {
    // Expiration runs before movement; countdown alone never dirties a stat row.
    internal sealed class AdvanceStatusesSystem : ISystem {
        private readonly StatStorage stats;
        public AdvanceStatusesSystem(StatStorage stats) => this.stats = stats;
        public World World { get; set; }
        public void OnAwake() => this.stats.Attach(this.World);
        public void OnUpdate(float deltaTime) { this.stats.Advance(deltaTime); this.stats.RecalculateDirty(); }
        public void Dispose() { }
    }
    internal sealed class RecalculateStatsSystem : ISystem {
        private readonly StatStorage stats;
        public RecalculateStatsSystem(StatStorage stats) => this.stats = stats;
        public World World { get; set; }
        public void OnAwake() { }
        public void OnUpdate(float deltaTime) => this.stats.RecalculateDirty();
        public void Dispose() { }
    }
    // After outcome resolution, before entity destruction and the external snapshot.
    internal sealed class CleanupStatusesSystem : ISystem {
        private readonly StatStorage stats;
        private readonly EntityLookup entities;
        private Stash<GameStateComponent> states;
        public CleanupStatusesSystem(StatStorage stats, EntityLookup entities) { this.stats = stats; this.entities = entities; }
        public World World { get; set; }
        public void OnAwake() => this.states = this.World.GetStash<GameStateComponent>();
        public void OnUpdate(float deltaTime) {
            var phase = this.states.Get(this.entities.gameState).phase;
            this.stats.ClearInvalidTargets(phase == SessionPhase.Results || phase == SessionPhase.Meta);
            this.stats.RecalculateDirty();
        }
        public void Dispose() { }
    }
}

using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.Spatial;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Systems {
    internal sealed class ArenaSessionSystem : ISystem {
        private readonly Game.Domain.SimulationSettings config;
        private readonly EntityLookup entities;
        private Stash<GameStateComponent> states;
        private Stash<PositionComponent> positions;
        private Stash<BattleStatisticsComponent> statistics;
        public ArenaSessionSystem(Game.Domain.SimulationSettings config, EntityLookup entities) {
            this.config = config;
            this.entities = entities;
        }
        public World World { get; set; }
        public void OnAwake() {
            this.states = this.World.GetStash<GameStateComponent>();
            this.positions = this.World.GetStash<PositionComponent>();
            this.statistics = this.World.GetStash<BattleStatisticsComponent>();
        }
        public void OnUpdate(float deltaTime) {
            var entity = this.entities.gameState;
            ref var state = ref this.states.Get(entity);
            switch (state.phase) {
                case SessionPhase.Initialization:
                    state.phase = SessionPhase.AwaitingEntry;
                    break;
                case SessionPhase.AwaitingEntry:
                    var position = this.positions.Get(this.entities.player).value;
                    if (ArenaGeometry.IsInside(this.config, position, this.config.playerRadius)) {
                        state.phase = SessionPhase.Combat;
                    }
                    break;
                case SessionPhase.Combat:
                    this.statistics.Get(entity).value.duration += deltaTime;
                    break;
                case SessionPhase.Results:
                    state.resultsTimer = Mathf.Max(0f, state.resultsTimer - deltaTime);
                    if (state.resultsTimer <= 0f) state.phase = SessionPhase.Meta;
                    break;
            }
        }
        public void Dispose() { }
    }
}

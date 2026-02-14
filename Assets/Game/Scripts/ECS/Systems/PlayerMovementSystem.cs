using Game.ECS.Components;
using Game.ECS.Core;
using Game.Domain;
using Game.Spatial;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Systems {
    internal sealed class PlayerMovementSystem : ISystem {
        private readonly Game.Domain.SimulationSettings config;
        private readonly EntityLookup entities;
        private readonly ObstacleGridLookup obstacles;
        private Stash<PositionComponent> positions;
        private Stash<MoveInputComponent> inputs;
        private Stash<MoveSpeedComponent> speeds;
        private Stash<GameStateComponent> states;
        public PlayerMovementSystem(Game.Domain.SimulationSettings config, EntityLookup entities, ObstacleGridLookup obstacles) {
            this.config = config;
            this.entities = entities;
            this.obstacles = obstacles;
        }
        public World World { get; set; }
        public void OnAwake() {
            this.positions = this.World.GetStash<PositionComponent>();
            this.inputs = this.World.GetStash<MoveInputComponent>();
            this.speeds = this.World.GetStash<MoveSpeedComponent>();
            this.states = this.World.GetStash<GameStateComponent>();
        }
        public void OnUpdate(float deltaTime) {
            if (GameStateHelper.IsGameOver(this.entities, this.states) ||
                !this.entities.TryGetPlayer(out var player)) return;
            var input = Vector2.ClampMagnitude(this.inputs.Get(player).value, 1f);
            var displacement = new Vector3(input.x, 0f, input.y) * (this.speeds.Get(player).value * deltaTime);
            ref var position = ref this.positions.Get(player);
            var closed = GameStateHelper.IsCombat(this.entities, this.states);
            var motion = new ActorMotionQuery(this.config.arenaHalfSize, this.config.playerRadius, closed, this.obstacles.AsNative());
            position.value = motion.Move(position.value, displacement);
        }
        public void Dispose() { }
    }
}

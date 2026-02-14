using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;

namespace Game.ECS.Systems {
    internal sealed class ChasePlayerSystem : ISystem {
        private readonly EntityLookup entities;
        private Filter actors;
        private Stash<MovementGoalComponent> goals;
        private Stash<PositionComponent> positions;
        private Stash<EnemyBehaviourComponent> behaviours;
        private Stash<GameStateComponent> states;
        public ChasePlayerSystem(EntityLookup entities) => this.entities = entities;
        public World World { get; set; }
        public void OnAwake() {
            this.actors = this.World.Filter.With<ChasePlayerTag>().With<MovementGoalComponent>()
                .With<EnemyBehaviourComponent>().Without<DestroyTag>().Build();
            this.goals = this.World.GetStash<MovementGoalComponent>();
            this.positions = this.World.GetStash<PositionComponent>();
            this.behaviours = this.World.GetStash<EnemyBehaviourComponent>();
            this.states = this.World.GetStash<GameStateComponent>();
        }
        public void OnUpdate(float dt) {
            if (!GameStateHelper.IsCombat(this.entities, this.states)) return;
            var target = this.positions.Get(this.entities.player).value;
            foreach (var actor in this.actors) this.goals.Set(actor, new MovementGoalComponent {
                position = target, stoppingDistance = this.behaviours.Get(actor).stopDistance, active = true
            });
        }
        public void Dispose() { }
    }
}

using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;

namespace Game.ECS.Weapons {
    internal sealed class PlayerAttackIntentSystem : ISystem {
        private readonly EntityLookup entities;
        private Stash<CombatInputComponent> input;
        private Stash<AttackIntentComponent> intents;
        public PlayerAttackIntentSystem(EntityLookup entities) => this.entities = entities;
        public World World { get; set; }
        public void OnAwake() { this.input = this.World.GetStash<CombatInputComponent>(); this.intents = this.World.GetStash<AttackIntentComponent>(); }
        public void OnUpdate(float deltaTime) {
            if (!this.entities.TryGetPlayer(out var player) || !this.intents.Has(player)) return;
            var input = this.input.Get(player);
            this.intents.Set(player, new AttackIntentComponent { held = input.attackHeld, aim = input.aim });
        }
        public void Dispose() { }
    }
    internal sealed class EnemyAttackIntentSystem : ISystem {
        private Filter actors;
        private Stash<AttackIntentComponent> intents;
        public World World { get; set; }
        public void OnAwake() {
            this.actors = this.World.Filter.With<EnemyTag>().With<WeaponComponent>().With<AttackIntentComponent>().Without<DestroyTag>().Build();
            this.intents = this.World.GetStash<AttackIntentComponent>();
        }
        public void OnUpdate(float deltaTime) {
            foreach (var actor in this.actors) this.intents.Set(actor, new AttackIntentComponent { held = true });
        }
        public void Dispose() { }
    }
}

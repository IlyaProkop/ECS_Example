using Game.Domain.Abilities;
using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Abilities {
    // Shared activation for every equipped actor; intent producers do not execute effects.
    internal sealed class AbilityActivationSystem : ISystem {
        private readonly EntityLookup entities;
        private readonly FrameBuffer<AbilityCast> casts;
        private Stash<AbilityComponent> abilities;
        private Filter owners;
        private Stash<DestroyTag> destroyed;
        private Stash<PositionComponent> positions;
        private Stash<DamageSourceComponent> sources;
        private Stash<HealthComponent> health;
        private Stash<GameStateComponent> states;
        private Stash<DamageMultiplierComponent> multipliers;
        public World World { get; set; }
        public AbilityActivationSystem(EntityLookup entities, FrameBuffer<AbilityCast> casts) {
            this.entities = entities; this.casts = casts;
        }
        public void OnAwake() {
            this.destroyed = this.World.GetStash<DestroyTag>();
            this.abilities = this.World.GetStash<AbilityComponent>();
            this.owners = this.World.Filter.With<AbilityComponent>().With<PositionComponent>()
                .With<HealthComponent>().With<DamageSourceComponent>().Build();
            this.positions = this.World.GetStash<PositionComponent>();
            this.sources = this.World.GetStash<DamageSourceComponent>();
            this.health = this.World.GetStash<HealthComponent>();
            this.states = this.World.GetStash<GameStateComponent>();
            this.multipliers = this.World.GetStash<DamageMultiplierComponent>();
        }
        public void OnUpdate(float deltaTime) {
            var combat = GameStateHelper.IsCombat(this.entities, this.states);
            foreach (var owner in this.owners) {
                ref var ability = ref this.abilities.Get(owner);
                var requested = ability.requestedSlots;
                ability.requestedSlots = 0; // Never retain requests across death or phase changes.
                if (!combat || this.destroyed.Has(owner) || this.health.Get(owner).current <= 0f) continue;
                for (var i = 0; i < ability.count; i++) {
                    var slot = ability.Get(i);
                    slot.remainingCooldown = Mathf.Max(0f, slot.remainingCooldown - deltaTime);
                    if ((requested & (1u << i)) != 0 && slot.remainingCooldown <= 0f) {
                        this.casts.Add(new AbilityCast(owner, slot.definitionId, this.positions.Get(owner).value,
                            this.sources.Get(owner), this.multipliers.Has(owner) ? this.multipliers.Get(owner).value : 1f));
                        slot.remainingCooldown = slot.cooldown;
                    }
                    ability.Set(i, slot);
                }
            }
        }

        public void Dispose() { }
    }
}

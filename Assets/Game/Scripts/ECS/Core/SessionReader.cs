using System;
using Game.Domain;
using Game.ECS.Components;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Core {
    internal sealed class SessionReader {
        private readonly EntityLookup entities;
        private readonly int enemyCount;
        private readonly Filter actors;
        private readonly Stash<ActorComponent> identities;
        private readonly Stash<PositionComponent> positions;
        private readonly Stash<RadiusComponent> radii;
        private readonly Stash<HealthComponent> health;
        private readonly Stash<ResourceComponent> resources;
        private readonly Stash<GameStateComponent> states;
        private readonly Stash<BattleStatisticsComponent> statistics;
        private readonly Stash<AbilityComponent> abilities;
        private readonly Stats.StatStorage statStorage;

        public SessionReader(World world, EntityLookup entities, int enemyCount, Stats.StatStorage statStorage) {
            this.statStorage = statStorage;
            this.entities = entities;
            this.enemyCount = enemyCount;
            this.actors = world.Filter.With<ActorComponent>().With<PositionComponent>().With<RadiusComponent>().Without<DestroyTag>().Build();
            this.identities = world.GetStash<ActorComponent>();
            this.positions = world.GetStash<PositionComponent>();
            this.radii = world.GetStash<RadiusComponent>();
            this.health = world.GetStash<HealthComponent>();
            this.resources = world.GetStash<ResourceComponent>();
            this.states = world.GetStash<GameStateComponent>();
            this.statistics = world.GetStash<BattleStatisticsComponent>();
            this.abilities = world.GetStash<AbilityComponent>();
        }
        public bool TryFindActor(int actorId, out Entity actor) {
            if (actorId > 0) foreach (var candidate in this.actors) {
                if (this.identities.Get(candidate).id != actorId) continue;
                actor = candidate; return true;
            }
            actor = default; return false;
        }
        public BattleSnapshot ReadSnapshot() {
            var state = this.states.Get(this.entities.gameState);
            var stats = this.statistics.Get(this.entities.gameState).value;
            var ability = this.abilities.Get(this.entities.player);
            return new BattleSnapshot(Mathf.CeilToInt(this.health.Get(this.entities.player).current),
                this.resources.Get(this.entities.player).coins, Math.Max(0, this.enemyCount - stats.kills),
                state.phase, state.outcome, stats, ability.remainingCooldown, this.positions.Get(this.entities.player).value,
                this.statStorage.ReadPlayer(this.entities.player), this.ReadAbilities(ability, state.phase));
        }
        private Game.Domain.Abilities.AbilityBarSnapshot ReadAbilities(in AbilityComponent ability, SessionPhase phase) {
            var alive = this.health.Get(this.entities.player).current > 0f;
            Game.Domain.Abilities.AbilitySlotSnapshot Read(int index) {
                var owner = this.abilities.Get(this.entities.player);
                if (index >= owner.count) return default;
                var slot = owner.Get(index);
                return new Game.Domain.Abilities.AbilitySlotSnapshot(slot.definitionId, slot.cooldown, slot.remainingCooldown,
                    alive && phase == SessionPhase.Combat && slot.remainingCooldown <= 0f);
            }
            return new Game.Domain.Abilities.AbilityBarSnapshot(ability.count, Read(0), Read(1), Read(2), Read(3));
        }
        public BattleResult ReadResult(string sessionId, int victoryReward) {
            var state = this.states.Get(this.entities.gameState);
            var stats = this.statistics.Get(this.entities.gameState).value;
            var reward = state.outcome == BattleOutcome.Victory ? checked(victoryReward + stats.coinsCollected) : 0;
            return new BattleResult(sessionId, state.outcome, stats, reward);
        }
        public int CopyActors(Span<ActorView> destination) {
            var count = 0;
            foreach (var entity in this.actors) {
                var actor = this.identities.Get(entity);
                var hp = this.health.Has(entity) ? this.health.Get(entity).current : 0f;
                if (actor.kind == ActorKind.Enemy && hp <= 0f) continue;
                destination[count++] = new ActorView(actor.id, actor.kind, this.positions.Get(entity).value, this.radii.Get(entity).value, hp, actor.visualId);
            }
            return count;
        }
    }
}

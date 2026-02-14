using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;

namespace Game.ECS.Systems {
    internal sealed class CoinPickupSystem : ISystem {
        private readonly EntityLookup entities;
        private Filter coinFilter;

        private Stash<PositionComponent> positionStash;
        private Stash<ResourceComponent> resourceStash;
        private Stash<RadiusComponent> radiusStash;
        private Stash<CoinComponent> coinStash;
        private Stash<DestroyTag> destroyStash;
        private Stash<GameStateComponent> gameStateStash;
        private Stash<BattleStatisticsComponent> statistics;

        public CoinPickupSystem(EntityLookup entities) {
            this.entities = entities;
        }

        public World World { get; set; }

        public void OnAwake() {
            this.statistics = this.World.GetStash<BattleStatisticsComponent>();
            this.coinFilter = this.World.Filter.With<CoinTag>().With<PositionComponent>().With<CoinComponent>().Without<DestroyTag>().Build();

            this.positionStash = this.World.GetStash<PositionComponent>();
            this.resourceStash = this.World.GetStash<ResourceComponent>();
            this.radiusStash = this.World.GetStash<RadiusComponent>();
            this.coinStash = this.World.GetStash<CoinComponent>();
            this.destroyStash = this.World.GetStash<DestroyTag>();
            this.gameStateStash = this.World.GetStash<GameStateComponent>();
        }

        public void OnUpdate(float deltaTime) {
            if (GameStateHelper.IsGameOver(this.entities, this.gameStateStash)) {
                return;
            }

            if (!this.entities.TryGetPlayer(out var playerEntity) ||
                !this.positionStash.Has(playerEntity) ||
                !this.radiusStash.Has(playerEntity) ||
                !this.resourceStash.Has(playerEntity)) {
                return;
            }

            var playerRadius = this.radiusStash.Get(playerEntity).value;
            var playerResource = this.resourceStash.Get(playerEntity);
            var playerPosition = this.positionStash.Get(playerEntity).value;

            foreach (var coinEntity in this.coinFilter) {
                var coinPosition = this.positionStash.Get(coinEntity).value;
                var pickupRadius = this.coinStash.Get(coinEntity).pickupRadius;
                var delta = coinPosition - playerPosition;
                delta.y = 0f;
                var distanceSqr = delta.sqrMagnitude;
                var totalRadius = pickupRadius + playerRadius;

                if (distanceSqr > totalRadius * totalRadius) {
                    continue;
                }

                playerResource.coins += 1;
                this.statistics.Get(this.entities.gameState).value.coinsCollected++;
                this.destroyStash.Add(coinEntity);
            }

            this.resourceStash.Set(playerEntity, playerResource);
        }

        public void Dispose() {
        }
    }
}

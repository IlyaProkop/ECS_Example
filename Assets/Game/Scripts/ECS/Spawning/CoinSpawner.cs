using Game.Domain;
using Game.ECS.Components;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Spawning {
    internal sealed class CoinSpawner : ICoinSpawner {
        private readonly ActorFactory actors;
        private readonly float radius, pickupRadius;
        private readonly Stash<CoinTag> coins;
        private readonly Stash<CoinComponent> pickups;
        public CoinSpawner(World world, ActorFactory actors, float radius, float pickupRadius) {
            this.actors = actors;
            this.radius = radius;
            this.pickupRadius = pickupRadius;
            this.coins = world.GetStash<CoinTag>();
            this.pickups = world.GetStash<CoinComponent>();
        }
        public Entity Spawn(Vector3 position) {
            var entity = this.actors.Create(ActorKind.Coin, new Vector3(position.x, this.radius, position.z), this.radius);
            this.coins.Add(entity);
            this.pickups.Set(entity, new CoinComponent { pickupRadius = this.pickupRadius });
            return entity;
        }
    }
}

using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Systems {
    internal sealed class EnemyContactDamageSystem : ISystem {
        private readonly EntityLookup entities;
        private readonly Game.Spatial.EnemySpatialIndex spatial;
        private readonly FrameBuffer<DamageRequest> requests;
        private Stash<PositionComponent> positions;
        private Stash<EnemyBehaviourComponent> behaviours;
        private Stash<DamageSourceComponent> sources;
        private Stash<GameStateComponent> states;
        private Stash<DamageMultiplierComponent> multipliers;
        public EnemyContactDamageSystem(EntityLookup entities, Game.Spatial.EnemySpatialIndex spatial, FrameBuffer<DamageRequest> requests) {
            this.entities = entities;
            this.spatial = spatial;
            this.requests = requests;
        }
        public World World { get; set; }
        public void OnAwake() {
            this.positions = this.World.GetStash<PositionComponent>();
            this.behaviours = this.World.GetStash<EnemyBehaviourComponent>();
            this.sources = this.World.GetStash<DamageSourceComponent>();
            this.states = this.World.GetStash<GameStateComponent>();
            this.multipliers = this.World.GetStash<DamageMultiplierComponent>();
        }
        public void OnUpdate(float deltaTime) {
            if (!GameStateHelper.IsCombat(this.entities, this.states) || deltaTime <= 0f) return;
            var player = this.entities.player;
            var position = this.positions.Get(player).value;
            foreach (var sample in this.spatial.QueryCircle(position, this.spatial.MaxAttackRange)) {
                var enemy = sample.Entity;
                if (!sample.IsAlive || !this.behaviours.Has(enemy)) continue;
                var behaviour = this.behaviours.Get(enemy);
                var reach = behaviour.attackRange;
                if ((this.positions.Get(enemy).value - position).sqrMagnitude > reach * reach) continue;
                this.requests.Add(new DamageRequest {
                    target = player, value = behaviour.contactDamagePerSecond * deltaTime * (this.multipliers.Has(enemy) ? this.multipliers.Get(enemy).value : 1f),
                    source = this.sources.Get(enemy), kind = DamageKind.Contact
                });
            }
        }
        public void Dispose() { }
    }
}

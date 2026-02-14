using Game.ECS.Components;
using Game.ECS.Core;
using Game.ECS.Spawning;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Weapons {
    internal struct WeaponFired { public bool Player; }
    // The same execution path for input and AI. No component refs cross the spawner boundary.
    internal sealed class WeaponFireSystem : ISystem {
        private readonly EntityLookup entities;
        private readonly Game.Spatial.EnemySpatialIndex spatial;
        private readonly WeaponCatalog catalog;
        private readonly IProjectileSpawner spawner;
        private readonly FrameBuffer<WeaponFired> fired;
        private Filter actors;
        private Stash<WeaponComponent> weapons;
        private Stash<AttackIntentComponent> intents;
        private Stash<PositionComponent> positions;
        private Stash<DamageSourceComponent> sources;
        private Stash<DamageMultiplierComponent> multipliers;
        private Stash<HealthComponent> health;
        private Stash<DestroyTag> destroyed;
        private Stash<PlayerTag> players;
        private Stash<GameStateComponent> states;
        private AttackTargeting targeting;
        public WeaponFireSystem(EntityLookup entities, Game.Spatial.EnemySpatialIndex spatial, WeaponCatalog catalog,
            IProjectileSpawner spawner, FrameBuffer<WeaponFired> fired) {
            this.entities = entities; this.spatial = spatial; this.catalog = catalog; this.spawner = spawner; this.fired = fired;
        }
        public World World { get; set; }
        public void OnAwake() {
            this.actors = this.World.Filter.With<WeaponComponent>().With<AttackIntentComponent>().With<PositionComponent>()
                .With<DamageSourceComponent>().With<HealthComponent>().Without<DestroyTag>().Build();
            this.weapons = this.World.GetStash<WeaponComponent>(); this.intents = this.World.GetStash<AttackIntentComponent>();
            this.positions = this.World.GetStash<PositionComponent>(); this.sources = this.World.GetStash<DamageSourceComponent>();
            this.multipliers = this.World.GetStash<DamageMultiplierComponent>(); this.health = this.World.GetStash<HealthComponent>();
            this.destroyed = this.World.GetStash<DestroyTag>(); this.players = this.World.GetStash<PlayerTag>();
            this.states = this.World.GetStash<GameStateComponent>();
            this.targeting = new AttackTargeting(this.World, this.entities, this.spatial);
        }
        public void OnUpdate(float deltaTime) {
            if (!GameStateHelper.IsCombat(this.entities, this.states)) return;
            foreach (var actor in this.actors) {
                if (this.health.Get(actor).current <= 0f) continue;
                var state = this.weapons.Get(actor);
                state.cooldown = Mathf.Max(0f, state.cooldown - deltaTime); this.weapons.Set(actor, state);
                var intent = this.intents.Get(actor);
                if (!intent.held || state.cooldown > 0f) continue;
                var weapon = this.catalog.Get(state.definitionIndex);
                if (!this.targeting.TryAim(actor, intent.aim, weapon.Definition.Range, out var aim)) continue;
                var projectile = weapon.Definition.Projectile;
                var position = this.positions.Get(actor).value; position.y = 0f;
                var source = this.sources.Get(actor);
                var multiplier = this.multipliers.Has(actor) ? this.multipliers.Get(actor).value : 1f;
                var player = this.players.Has(actor);
                this.spawner.Spawn(new ProjectileSpawn(position, aim.Direction, projectile.Speed, projectile.Damage * multiplier,
                    projectile.Radius, projectile.Lifetime, source, aim.Target, weapon.ProjectileIndex, aim.TargetActorId));
                // A custom spawner may replace equipment. Never write back its old definition.
                if (!actor.IsNullOrDisposed() && !this.destroyed.Has(actor) && this.weapons.Has(actor)) {
                    var current = this.weapons.Get(actor);
                    current.cooldown = Mathf.Max(current.cooldown, weapon.Definition.Interval); this.weapons.Set(actor, current);
                }
                this.fired.Add(new WeaponFired { Player = player });
            }
        }
        public void Dispose() { this.targeting = null; }
    }
    internal sealed class WeaponStatisticsSystem : ISystem {
        private readonly EntityLookup entities;
        private readonly FrameBuffer<WeaponFired> fired;
        private Stash<BattleStatisticsComponent> statistics;
        public WeaponStatisticsSystem(EntityLookup entities, FrameBuffer<WeaponFired> fired) { this.entities = entities; this.fired = fired; }
        public World World { get; set; }
        public void OnAwake() => this.statistics = this.World.GetStash<BattleStatisticsComponent>();
        public void OnUpdate(float deltaTime) {
            try {
                foreach (ref readonly var fact in this.fired.Items) if (fact.Player) this.statistics.Get(this.entities.gameState).value.attacks++;
            } finally { this.fired.Clear(); }
        }
        public void Dispose() => this.fired.Clear();
    }
}

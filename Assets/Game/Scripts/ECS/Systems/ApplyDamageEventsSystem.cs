using System;
using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;

namespace Game.ECS.Systems {
    // Only health authority for damage. Statistics consumes facts in the next phase.
    internal sealed class ApplyDamageEventsSystem : ISystem {
        private readonly EntityLookup entities;
        private readonly FrameBuffer<DamageRequest> requests;
        private readonly FrameBuffer<DamageApplied> applied;
        private readonly DamagePipeline pipeline;
        private readonly Random random;
        private Stash<HealthComponent> health;
        private Stash<DefenseComponent> defenses;
        private Stash<PlayerTag> players;
        private Stash<DestroyTag> destroyed;
        private Stash<GameStateComponent> states;
        public ApplyDamageEventsSystem(EntityLookup entities, FrameBuffer<DamageRequest> requests,
            FrameBuffer<DamageApplied> applied, DamagePipeline pipeline, Random random) {
            this.entities = entities; this.requests = requests; this.applied = applied;
            this.pipeline = pipeline; this.random = random;
        }
        public World World { get; set; }
        public void OnAwake() {
            this.health = this.World.GetStash<HealthComponent>();
            this.defenses = this.World.GetStash<DefenseComponent>();
            this.players = this.World.GetStash<PlayerTag>();
            this.destroyed = this.World.GetStash<DestroyTag>();
            this.states = this.World.GetStash<GameStateComponent>();
        }
        public void OnUpdate(float deltaTime) {
            try {
                if (this.states.Get(this.entities.gameState).phase != SessionPhase.Combat) return;
                foreach (ref readonly var request in this.requests.Items) {
                    var target = request.target;
                    // Weak target, strict lifetime check. Attribution is a value snapshot.
                    if (target == null || target.IsNullOrDisposed() || !this.health.Has(target) || this.destroyed.Has(target)) continue;
                    var currentHealth = this.health.Get(target).current;
                    if (currentHealth <= 0f) continue;
                    var damage = new DamageCalculation {
                        amount = request.value,
                        armor = this.defenses.Has(target) ? this.defenses.Get(target).armor : 0f,
                        criticalChance = request.source.criticalChance,
                        criticalMultiplier = request.source.criticalMultiplier,
                        criticalRoll = request.source.criticalChance > 0f ? (float)this.random.NextDouble() : 1f,
                        kind = request.kind
                    };
                    var requestedAmount = this.pipeline.Resolve(ref damage, currentHealth);
                    var nextHealth = currentHealth - requestedAmount;
                    var amount = currentHealth - nextHealth;
                    if (amount <= 0f) continue;
                    // No component ref crosses an extensible modifier callback.
                    this.health.Get(target).current = nextHealth;
                    this.applied.Add(new DamageApplied {
                        amount = amount, sourceTeam = request.source.team,
                        targetIsPlayer = this.players.Has(target), critical = damage.isCritical
                    });
                }
            } finally { this.requests.Clear(); }
        }
        public void Dispose() { }
    }
}

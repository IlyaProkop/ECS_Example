using Game.Domain.Stats;
using Game.ECS.Components;
using Game.ECS.Stats;
using Game.Spatial;
using Scellecs.Morpeh;

namespace Game.ECS.Abilities {
    // One bounded scan at cast time. Recipient eligibility belongs to status storage.
    internal sealed class AreaStatusEffect : IAbilityEffect {
        private readonly AreaStatusDefinition definition;
        private readonly StatStorage stats;
        private readonly CompiledStatus status;
        private readonly ObstacleGridLookup obstacles;
        private readonly Filter targets;
        private readonly Stash<ActorComponent> actors;
        private readonly Stash<PositionComponent> positions;
        public AreaStatusEffect(AreaStatusDefinition definition, World world, StatStorage stats, ObstacleGridLookup obstacles) {
            this.definition = definition; this.stats = stats; this.obstacles = obstacles;
            this.status = stats.Compile(definition.Status);
            this.targets = world.Filter.With<ActorComponent>().With<PositionComponent>().With<StatOwnerComponent>().Without<DestroyTag>().Build();
            this.actors = world.GetStash<ActorComponent>(); this.positions = world.GetStash<PositionComponent>();
        }
        public void Execute(in AbilityCast cast) {
            var lookup = this.obstacles.AsNative();
            var source = new EffectSource(cast.source.owner, this.status.Definition.Id);
            foreach (var target in this.targets) {
                if (((int)this.definition.Targets & (1 << (int)this.actors.Get(target).kind)) == 0 || !this.stats.CanReceive(target)) continue;
                var position = this.positions.Get(target).value;
                var delta = position - cast.position; delta.y = 0f;
                if (delta.sqrMagnitude <= this.definition.Radius * this.definition.Radius &&
                    ObstacleQueries.HasLineOfSight(cast.position, position, 0f, lookup))
                    this.stats.TryApply(target, source, this.status, cast.source, out _);
            }
        }
        public void Dispose() { }
    }
}

using Game.Domain;
using Game.Domain.Abilities;
using Game.ECS.Core;
using Scellecs.Morpeh;

namespace Game.ECS.Abilities {
    internal sealed class AreaDamageEffect : IAbilityEffect {
        private readonly AreaDamageDefinition definition;
        private readonly HostileAreaQuery targets;
        private readonly FrameBuffer<DamageRequest> requests;
        public AreaDamageEffect(AreaDamageDefinition definition, HostileAreaQuery targets, FrameBuffer<DamageRequest> requests) {
            this.definition = definition; this.targets = targets; this.requests = requests;
        }
        public void Execute(in AbilityCast cast) {
            var visitor = new DamageVisitor { requests = this.requests, template = new DamageRequest {
                value = this.definition.Damage * cast.damageMultiplier, source = cast.source, kind = DamageKind.Area
            } };
            this.targets.Visit(cast.source.team, cast.position, this.definition.Radius, ref visitor);
        }
        private struct DamageVisitor : IAreaTargetVisitor {
            public FrameBuffer<DamageRequest> requests;
            public DamageRequest template;
            public void Visit(Entity target) { this.template.target = target; this.requests.Add(this.template); }
        }
        public void Dispose() { }
    }
}

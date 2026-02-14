using System;
using System.Collections.Generic;
using Game.Domain.Abilities;
using Game.ECS.Core;
using Scellecs.Morpeh;

namespace Game.ECS.Abilities {
    internal sealed class AbilityExecutionSystem : ISystem {
        private readonly FrameBuffer<AbilityCast> casts;
        private readonly Dictionary<int, IAbilityEffect[]> effects = new();
        public World World { get; set; }
        public AbilityExecutionSystem(FrameBuffer<AbilityCast> casts, IEnumerable<AbilityDefinition> definitions, AbilityEffectRegistry registry) {
            this.casts = casts;
            try {
                foreach (var definition in definitions) {
                    if (this.effects.ContainsKey(definition.Id)) throw new ArgumentException("Duplicate ability ID.");
                    this.effects.Add(definition.Id, registry.Compile(definition));
                }
            } catch { this.Dispose(); throw; }
        }
        public void OnAwake() { }
        public void OnUpdate(float deltaTime) {
            foreach (ref readonly var cast in this.casts.Items)
                foreach (var effect in this.effects[cast.abilityId]) effect.Execute(cast);
        }
        public void Dispose() {
            foreach (var batch in this.effects.Values) foreach (var effect in batch) effect.Dispose();
            this.effects.Clear();
        }
    }
}

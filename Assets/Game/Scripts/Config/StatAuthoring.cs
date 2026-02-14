using System;
using Game.Domain.Abilities;
using Game.Domain.Stats;
using UnityEngine;

namespace Game.Config {
    [Serializable]
    public sealed class StatDefinitionAuthoring {
        [Min(1)] public int id;
        public float defaultValue, min, max;
        public StatDefinition CreateDefinition() => new StatDefinition(this.id, this.defaultValue, this.min, this.max);
        public static StatDefinitionAuthoring[] CreateDefaults() {
            var definitions = StatCatalog.Default.Definitions;
            var result = new StatDefinitionAuthoring[definitions.Count];
            for (var i = 0; i < result.Length; i++) {
                var value = definitions[i];
                result[i] = new StatDefinitionAuthoring { id = value.Id, defaultValue = value.DefaultValue, min = value.Min, max = value.Max };
            }
            return result;
        }
    }
    [Serializable]
    public sealed class StatModifierAuthoring {
        [Min(1)] public int statId;
        public StatOperation operation;
        public float value;
        public int priority;
        public StatModifierDefinition CreateDefinition() => new StatModifierDefinition(this.statId, this.operation, this.value, this.priority);
    }
    [Serializable]
    public sealed class AreaStatusAuthoring : AbilityEffectAuthoring {
        [Min(.001f)] public float radius = 5f;
        public StatusTargets targets = StatusTargets.Enemy;
        public StatusEffectAsset status;
        public override AbilityEffectDefinition CreateDefinition() => new AreaStatusDefinition(this.radius, this.targets,
            this.status != null ? this.status.CreateDefinition() : throw new InvalidOperationException("Area effect requires a status."));
    }
    [Serializable]
    public sealed class SelfStatusAuthoring : AbilityEffectAuthoring {
        [Tooltip("Optional shared status. When assigned, its definition overrides the inline fields.")]
        public StatusEffectAsset status;
        [Min(1)] public int id = BuiltInStatuses.EmpowermentId;
        [Min(0.01f)] public float duration = BuiltInStatuses.Duration;
        public StatusStacking stacking = StatusStacking.Refresh;
        [Range(1, 16)] public int maxStacks = 1;
        public StatModifierAuthoring[] modifiers = {
            new StatModifierAuthoring { statId = StatIds.MoveSpeed, operation = StatOperation.Multiply, value = BuiltInStatuses.SpeedMultiplier },
            new StatModifierAuthoring { statId = StatIds.Armor, operation = StatOperation.Add, value = BuiltInStatuses.ArmorBonus }
        };
        public override AbilityEffectDefinition CreateDefinition() {
            if (this.status != null) return new SelfStatusDefinition(this.status.CreateDefinition());
            if (this.modifiers == null) throw new InvalidOperationException("Missing status modifiers.");
            var values = new StatModifierDefinition[this.modifiers.Length];
            for (var i = 0; i < values.Length; i++) values[i] = this.modifiers[i]?.CreateDefinition()
                ?? throw new InvalidOperationException("Missing stat modifier.");
            return new SelfStatusDefinition(new StatusEffectDefinition(this.id, this.duration, this.stacking, this.maxStacks, values));
        }
    }
}

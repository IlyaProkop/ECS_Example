using System;
using System.Collections.ObjectModel;
using Game.Domain.Abilities;

namespace Game.Domain.Stats {
    public enum StatusStacking { Refresh, Independent }
    public sealed class StatusEffectDefinition {
        public int Id { get; }
        public float Duration { get; }
        public StatusStacking Stacking { get; }
        public int MaxStacks { get; }
        public ReadOnlyCollection<StatModifierDefinition> Modifiers { get; }
        public PeriodicDamageDefinition PeriodicDamage { get; }
        public StatusEffectDefinition(int id, float duration, StatusStacking stacking, int maxStacks,
            params StatModifierDefinition[] modifiers) : this(id, duration, stacking, maxStacks, null, modifiers) { }
        public StatusEffectDefinition(int id, float duration, StatusStacking stacking, int maxStacks,
            PeriodicDamageDefinition periodicDamage, params StatModifierDefinition[] modifiers) {
            if (id < 1) throw new ArgumentOutOfRangeException(nameof(id));
            StatValidation.Finite(duration);
            if (duration <= 0f) throw new ArgumentOutOfRangeException(nameof(duration));
            if (stacking < StatusStacking.Refresh || stacking > StatusStacking.Independent ||
                maxStacks < 1 || maxStacks > 16 || (stacking == StatusStacking.Refresh && maxStacks != 1))
                throw new ArgumentException("Refresh has one stack; independent effects support 1 to 16 stacks per source.");
            if (modifiers == null || modifiers.Length > 16 || (modifiers.Length == 0 && periodicDamage == null))
                throw new ArgumentException("A status requires modifiers or a periodic effect; at most 16 modifiers.");
            var copy = (StatModifierDefinition[])modifiers.Clone();
            foreach (var modifier in copy) {
                // Validate default(struct) and deserialized values as well as constructor-created values.
                _ = new StatModifierDefinition(modifier.StatId, modifier.Operation, modifier.Value, modifier.Priority);
            }
            this.Id = id; this.Duration = duration; this.Stacking = stacking; this.MaxStacks = maxStacks;
            this.Modifiers = Array.AsReadOnly(copy);
            this.PeriodicDamage = periodicDamage;
        }
    }
    public sealed class SelfStatusDefinition : AbilityEffectDefinition {
        public StatusEffectDefinition Status { get; }
        public SelfStatusDefinition(StatusEffectDefinition status) => this.Status = status ?? throw new ArgumentNullException(nameof(status));
        public override int GetDamageCapacity(int targetCapacity) => 0;
        public override bool UsesPeriodicDamage => this.Status.PeriodicDamage != null;
    }
    public static class BuiltInStatuses {
        public const int EmpowermentId = 1;
        public const float Duration = 3f;
        public const float SpeedMultiplier = 1.35f;
        public const float ArmorBonus = 30f;
        public static StatusEffectDefinition CreateEmpowerment() => new StatusEffectDefinition(EmpowermentId, Duration,
            StatusStacking.Refresh, 1,
            new StatModifierDefinition(StatIds.MoveSpeed, StatOperation.Multiply, SpeedMultiplier),
            new StatModifierDefinition(StatIds.Armor, StatOperation.Add, ArmorBonus));
    }
}

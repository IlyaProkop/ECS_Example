using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Domain.Stats {
    public static class StatIds {
        public const int MoveSpeed = 1;
        public const int Armor = 2;
        public const int DamageMultiplier = 3;
    }
    public sealed class StatDefinition {
        public int Id { get; }
        public float DefaultValue { get; }
        public float Min { get; }
        public float Max { get; }
        public StatDefinition(int id, float defaultValue, float min, float max) {
            if (id < 1) throw new ArgumentOutOfRangeException(nameof(id));
            StatValidation.Finite(defaultValue); StatValidation.Finite(min); StatValidation.Finite(max);
            if (min > max || defaultValue < min || defaultValue > max) throw new ArgumentException("Invalid stat range/default.");
            this.Id = id; this.DefaultValue = defaultValue; this.Min = min; this.Max = max;
        }
        public float Clamp(double value) => (float)Math.Clamp(value, this.Min, this.Max);
    }
    public sealed class StatCatalog {
        private readonly Dictionary<int, int> indices = new Dictionary<int, int>();
        public ReadOnlyCollection<StatDefinition> Definitions { get; }
        public static StatCatalog Default { get; } = new StatCatalog(
            new StatDefinition(StatIds.MoveSpeed, 0f, 0f, 100f), new StatDefinition(StatIds.Armor, 0f, 0f, 10000f),
            new StatDefinition(StatIds.DamageMultiplier, 1f, 0f, 100f));
        public StatCatalog(params StatDefinition[] definitions) {
            if (definitions == null || definitions.Length == 0 || definitions.Length > 64) throw new ArgumentException("A stat catalog supports 1 to 64 entries.");
            var copy = (StatDefinition[])definitions.Clone();
            for (var i = 0; i < copy.Length; i++) {
                if (copy[i] == null || !this.indices.TryAdd(copy[i].Id, i)) throw new ArgumentException("Missing or duplicate stat definition.");
            }
            this.Definitions = Array.AsReadOnly(copy);
        }
        public int IndexOf(int id) => this.indices.TryGetValue(id, out var index)
            ? index : throw new ArgumentException($"Unknown stat ID {id}.", nameof(id));
        public bool TryIndexOf(int id, out int index) => this.indices.TryGetValue(id, out index);
    }
    public enum StatOperation { Add, Multiply, Override }
    public readonly struct StatModifierDefinition {
        public readonly int StatId;
        public readonly StatOperation Operation;
        public readonly float Value;
        public readonly int Priority;
        public StatModifierDefinition(int statId, StatOperation operation, float value, int priority = 0) {
            if (statId < 1) throw new ArgumentOutOfRangeException(nameof(statId));
            if (operation < StatOperation.Add || operation > StatOperation.Override) throw new ArgumentOutOfRangeException(nameof(operation));
            StatValidation.Finite(value);
            if (operation == StatOperation.Multiply && value < 0f) throw new ArgumentOutOfRangeException(nameof(value));
            this.StatId = statId; this.Operation = operation; this.Value = value; this.Priority = priority;
        }
    }
    internal static class StatValidation {
        public static void Finite(float value) {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}

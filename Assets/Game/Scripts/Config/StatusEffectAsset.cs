using System;
using Game.Domain;
using Game.Domain.Stats;
using UnityEngine;

namespace Game.Config {
    [CreateAssetMenu(menuName = "Game/Combat/Status", fileName = "Status")]
    public sealed class StatusEffectAsset : ScriptableObject {
        [Min(1)] public int id = 10;
        [Min(.001f)] public float duration = 3f;
        public StatusStacking stacking;
        [Range(1, 16)] public int maxStacks = 1;
        public StatModifierAuthoring[] modifiers = Array.Empty<StatModifierAuthoring>();
        public bool periodicDamage;
        [Min(1f / 60f)] public float tickInterval = .5f;
        [Min(0f)] public float tickDamage = 5f;
        public DamageKind damageKind = DamageKind.Periodic;
        public StatusEffectDefinition CreateDefinition() {
            if (this.modifiers == null) throw new InvalidOperationException("Missing status modifiers.");
            var values = new StatModifierDefinition[this.modifiers.Length];
            for (var i = 0; i < values.Length; i++) values[i] = this.modifiers[i]?.CreateDefinition()
                ?? throw new InvalidOperationException("Missing stat modifier.");
            return new StatusEffectDefinition(this.id, this.duration, this.stacking, this.maxStacks,
                this.periodicDamage ? new PeriodicDamageDefinition(this.tickInterval, this.tickDamage, this.damageKind) : null, values);
        }
    }
}

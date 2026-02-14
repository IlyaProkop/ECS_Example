using System;
using Game.Domain.Abilities;
using UnityEngine;

namespace Game.Config {
    [CreateAssetMenu(menuName = "Game/Ability", fileName = "Ability")]
    public sealed class AbilityAsset : ScriptableObject {
        public const string DefaultDisplayName = "Ударная волна";
        [Min(1)] public int id = BuiltInAbilities.ShockwaveId;
        [Min(0.001f)] public float cooldown = BuiltInAbilities.ShockwaveCooldown;
        [SerializeReference] public AbilityEffectAuthoring[] effects = { new AreaDamageAuthoring(), new SelfStatusAuthoring() };
        [Header("Presentation")]
        public string displayName = DefaultDisplayName;
        [Min(0.1f)] public float ringRadius = BuiltInAbilities.ShockwaveRadius;

        public AbilityDefinition CreateDefinition() {
            if (this.effects == null) throw new InvalidOperationException("Ability effects are missing.");
            var definitions = new AbilityEffectDefinition[this.effects.Length];
            for (var i = 0; i < definitions.Length; i++) {
                if (this.effects[i] == null) throw new InvalidOperationException("Ability contains a missing effect.");
                definitions[i] = this.effects[i].CreateDefinition();
            }
            return new AbilityDefinition(this.id, this.cooldown, definitions);
        }
    }

    [Serializable]
    public abstract class AbilityEffectAuthoring {
        public abstract AbilityEffectDefinition CreateDefinition();
    }

    [Serializable]
    public sealed class AreaDamageAuthoring : AbilityEffectAuthoring {
        [Min(0.1f)] public float radius = BuiltInAbilities.ShockwaveRadius;
        [Min(0f)] public float damage = BuiltInAbilities.ShockwaveDamage;
        public override AbilityEffectDefinition CreateDefinition() => new AreaDamageDefinition(this.radius, this.damage);
    }
}

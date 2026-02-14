using System;
using System.Collections.Generic;
using Game.Domain.Abilities;
using Game.ECS.Components;
using Game.ECS.Core;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Abilities {
    internal readonly struct AbilityCast {
        public readonly Entity caster;
        public readonly int abilityId;
        public readonly Vector3 position;
        public readonly DamageSourceComponent source;
        public readonly float damageMultiplier;
        public AbilityCast(Entity caster, int abilityId, Vector3 position, DamageSourceComponent source, float damageMultiplier = 1f) {
            this.caster = caster;
            this.abilityId = abilityId;
            this.position = position;
            this.source = source;
            this.damageMultiplier = damageMultiplier;
        }
    }

    internal interface IAbilityEffect : IDisposable {
        void Execute(in AbilityCast cast);
    }

    // Registration happens during composition. No type lookup or allocation in a cast.
    internal sealed class AbilityEffectRegistry {
        private readonly EffectRegistry<AbilityEffectDefinition, IAbilityEffect> registry = new();
        public void Register<T>(Func<T, IAbilityEffect> factory) where T : AbilityEffectDefinition => this.registry.Register(factory);
        public IAbilityEffect[] Compile(AbilityDefinition definition) => this.registry.Compile(definition.Effects);
    }
}

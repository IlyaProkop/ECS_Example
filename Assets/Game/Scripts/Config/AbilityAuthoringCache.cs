using System;
using System.Collections.Generic;
using Game.Domain.Abilities;

namespace Game.Config {
    internal sealed class AbilityAuthoringCache {
        private readonly Dictionary<AbilityAsset, AbilityDefinition> definitions = new();
        public AbilityDefinition Resolve(AbilityAsset asset) {
            if (asset == null) throw new InvalidOperationException("Missing ability asset.");
            if (!this.definitions.TryGetValue(asset, out var definition)) {
                definition = asset.CreateDefinition(); this.definitions.Add(asset, definition);
            }
            return definition;
        }
        public AbilityLoadout Loadout(AbilityAsset[] assets, AbilityDefinition primary = null) {
            var slots = new AbilityDefinition[(assets?.Length ?? 0) + (primary == null ? 0 : 1)];
            var offset = primary == null ? 0 : 1;
            if (primary != null) slots[0] = primary;
            for (var i = offset; i < slots.Length; i++) slots[i] = this.Resolve(assets[i - offset]);
            return new AbilityLoadout(slots);
        }
    }
}

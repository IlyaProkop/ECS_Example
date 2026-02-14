using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Domain {
    // All loadout choices are immutable and budgeted before creating the world.
    public sealed class WeaponSet {
        private readonly Dictionary<int, int> indices = new();
        public ReadOnlyCollection<WeaponDefinition> Definitions { get; }
        public WeaponSet(WeaponDefinition player, IReadOnlyList<EnemyDefinition> enemies, WeaponDefinition[] additional) {
            var definitions = new List<WeaponDefinition>();
            void Add(WeaponDefinition definition) {
                if (definition == null) throw new ArgumentException("Missing weapon definition.");
                if (this.indices.TryGetValue(definition.Id, out var index)) {
                    if (!ReferenceEquals(definitions[index], definition)) throw new ArgumentException("Different weapons share one ID.");
                    return;
                }
                if (definitions.Count == 64) throw new ArgumentException("A session supports at most 64 weapon definitions.");
                this.indices.Add(definition.Id, definitions.Count); definitions.Add(definition);
            }
            Add(player);
            foreach (var enemy in enemies) if (enemy.Weapon != null) Add(enemy.Weapon);
            foreach (var weapon in additional ?? Array.Empty<WeaponDefinition>()) Add(weapon);
            this.Definitions = definitions.AsReadOnly();
        }
        public bool TryIndexOf(int id, out int index) => this.indices.TryGetValue(id, out index);
        public int IndexOf(int id) => this.TryIndexOf(id, out var index) ? index : throw new ArgumentException("Weapon is not registered in this session.");
    }
}

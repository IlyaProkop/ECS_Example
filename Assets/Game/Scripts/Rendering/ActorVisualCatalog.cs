using System;
using System.Collections.Generic;
using Game.Config;
using Game.Domain;
using UnityEngine;

namespace Game.Rendering {
    // Validated snapshot of authored values. Meshes/templates are borrowed assets; batch
    // materials are owned by the renderer. A slot is local to this catalog's lifetime.
    internal sealed class ActorVisualCatalog {
        private readonly Dictionary<int, int> slots;
        private readonly ActorVisual[] entries;
        public int Count => this.entries.Length;
        public ref readonly ActorVisual this[int slot] => ref this.entries[slot];
        public int SlotFor(int id) => this.slots.TryGetValue(id, out var slot) ? slot :
            throw new ArgumentException($"Actor visual {id} is missing from the catalog.", nameof(id));

        public ActorVisualCatalog(GameConfig config) {
            var source = config.actorVisuals;
            if (source == null || source.Length == 0) throw new ArgumentException("Actor visual catalog is empty.", nameof(config));
            this.slots = new Dictionary<int, int>(source.Length);
            this.entries = new ActorVisual[source.Length];
            // Validate before creating any temporary primitives or publishing the catalog.
            for (var i = 0; i < source.Length; i++) {
                var item = source[i];
                if (item == null || item.id <= 0 || !Finite(item.height) || item.height < 0 || !Finite(item.elevation) ||
                    !Positive(item.meshScale.x) || !Positive(item.meshScale.y) || !Positive(item.meshScale.z) ||
                    !Finite(item.color.r) || !Finite(item.color.g) || !Finite(item.color.b) || !Finite(item.color.a) ||
                    (item.mesh == null && !Enum.IsDefined(typeof(PrimitiveType), item.primitive)))
                    throw new ArgumentException($"Invalid actor visual at index {i}.", nameof(config));
                this.slots.Add(item.id, i);
            }
            this.SlotFor(ActorVisualIds.Player); this.SlotFor(ActorVisualIds.Projectile); this.SlotFor(ActorVisualIds.Coin);
            foreach (var enemy in config.enemyTypes) this.SlotFor(enemy.visualId);
            var primitives = new Dictionary<PrimitiveType, Mesh>();
            for (var i = 0; i < source.Length; i++) {
                var item = source[i];
                var mesh = item.mesh;
                if (mesh == null && !primitives.TryGetValue(item.primitive, out mesh)) {
                    mesh = InstancedRenderHelper.LoadPrimitiveMesh(item.primitive);
                    primitives.Add(item.primitive, mesh);
                }
                if (mesh == null) throw new ArgumentException($"Visual {item.id} has no mesh.", nameof(config));
                this.entries[i] = new ActorVisual(item, mesh);
            }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Positive(float value) => Finite(value) && value > 0;
    }

    internal readonly struct ActorVisual {
        public readonly Mesh Mesh;
        public readonly Material Template;
        public readonly Color Color;
        public readonly bool CastShadows, ReceiveShadows;
        private readonly Bounds meshBounds;
        private readonly Vector3 meshScale;
        private readonly float height, elevation;
        public ActorVisual(ActorVisualAuthoring source, Mesh mesh) {
            this.Mesh = mesh; this.Template = source.materialTemplate; this.Color = source.color;
            this.CastShadows = source.castShadows; this.ReceiveShadows = source.receiveShadows;
            this.meshBounds = mesh.bounds; this.meshScale = source.meshScale;
            this.height = source.height; this.elevation = source.elevation;
        }
        public Bounds Geometry(in ActorView actor, out Vector3 position, out Vector3 scale) {
            var diameter = actor.radius * 2f;
            var height = this.height > 0 ? this.height : diameter;
            position = actor.position;
            position.y = this.elevation >= 0 ? this.elevation : height * 0.5f;
            scale = Vector3.Scale(new Vector3(diameter, height, diameter), this.meshScale);
            return new Bounds(position + Vector3.Scale(this.meshBounds.center, scale), Vector3.Scale(this.meshBounds.size, scale));
        }
    }
}

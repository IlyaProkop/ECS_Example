using System;
using Game.Domain.Abilities;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Rendering {
    // Cosmetic budget: reuse the oldest ring when all slots are active.
    // Every event is consumed; simulation damage/statistics do not depend on this budget.
    internal sealed class AbilityCuePresenter : IDisposable {
        private const float Duration = 0.35f;
        private readonly LineRenderer[] rings;
        private readonly float[] remaining;
        private readonly Vector3[] positions;
        private readonly Vector3[] points = new Vector3[65];
        private readonly float radius;
        private readonly int abilityId;
        private Material material;
        private long lastSequence;
        private int nextSlot;

        public AbilityCuePresenter(Transform root, int capacity, int abilityId, float radius) {
            this.rings = new LineRenderer[capacity];
            this.remaining = new float[capacity];
            this.positions = new Vector3[capacity];
            this.abilityId = abilityId;
            this.radius = radius;
            try {
                this.material = InstancedRenderHelper.CreateInstancedMaterial(new Color(0.3f, 0.85f, 1f));
                for (var i = 0; i < capacity; i++) {
                    var go = new GameObject("AbilityRing");
                    go.transform.SetParent(root, false);
                    var ring = this.rings[i] = go.AddComponent<LineRenderer>();
                    ring.sharedMaterial = this.material;
                    ring.positionCount = this.points.Length;
                    ring.widthMultiplier = 0.12f;
                    ring.useWorldSpace = true;
                    ring.shadowCastingMode = ShadowCastingMode.Off;
                    ring.enabled = false;
                }
            } catch { this.Dispose(); throw; }
        }
        public void Render(ReadOnlySpan<AbilityCastEvent> events, float deltaTime, bool visible) {
            for (var i = 0; i < this.remaining.Length; i++) this.remaining[i] = Mathf.Max(0f, this.remaining[i] - deltaTime);
            foreach (ref readonly var item in events) {
                if (item.sequence <= this.lastSequence) continue;
                this.lastSequence = item.sequence;
                if (item.abilityId != this.abilityId) continue;
                var slot = this.nextSlot;
                this.nextSlot = (slot + 1) % this.rings.Length;
                this.remaining[slot] = Duration;
                this.positions[slot] = item.position;
            }
            for (var slot = 0; slot < this.rings.Length; slot++) {
                var ring = this.rings[slot];
                ring.enabled = visible && this.remaining[slot] > 0f;
                if (!ring.enabled) continue;
                var size = this.radius * (1f - this.remaining[slot] / Duration);
                for (var i = 0; i < this.points.Length; i++) {
                    var angle = i * Mathf.PI * 2f / (this.points.Length - 1);
                    this.points[i] = this.positions[slot] + new Vector3(Mathf.Cos(angle) * size, 0.15f, Mathf.Sin(angle) * size);
                }
                ring.SetPositions(this.points);
            }
        }
        public void Dispose() {
            foreach (var ring in this.rings) if (ring != null) UnityEngine.Object.Destroy(ring.gameObject);
            if (this.material != null) UnityEngine.Object.Destroy(this.material);
        }
    }
}

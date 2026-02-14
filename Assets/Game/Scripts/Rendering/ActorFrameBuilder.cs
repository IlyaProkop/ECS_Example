using System;
using Game.Config;
using Game.Domain;
using Game.UI;
using UnityEngine;

namespace Game.Rendering {
    internal readonly struct ActorRenderInstance {
        public readonly int Batch;
        public readonly Matrix4x4 Matrix;
        public ActorRenderInstance(int batch, Matrix4x4 matrix) { this.Batch = batch; this.Matrix = matrix; }
    }

    // Prepares borrowed value buffers. Reads camera projection; never submits draws or changes UI.
    internal sealed class ActorFrameBuilder {
        private readonly ActorRenderInstance[] instances;
        private readonly EnemyHealthLabel[] labels;
        private readonly Plane[] frustum = new Plane[6];
        private readonly float labelDistanceSquared;
        private readonly ActorVisualCatalog visuals;
        private int instanceCount, labelCount;
        public ReadOnlySpan<ActorRenderInstance> Instances => this.instances.AsSpan(0, this.instanceCount);
        public ReadOnlySpan<EnemyHealthLabel> Labels => this.labels.AsSpan(0, this.labelCount);

        public ActorFrameBuilder(GameConfig config, int capacity, int labelCapacity, ActorVisualCatalog visuals = null) {
            this.visuals = visuals ?? new ActorVisualCatalog(config);
            this.instances = new ActorRenderInstance[capacity];
            this.labels = new EnemyHealthLabel[labelCapacity];
            this.labelDistanceSquared = config.enemyWorldHpCullDistance * config.enemyWorldHpCullDistance;
        }
        public void Build(ReadOnlySpan<ActorView> actors, in BattleSnapshot snapshot, Camera camera, Vector2 screenSize) {
            if (actors.Length > this.instances.Length) throw new ArgumentException("Render frame capacity exceeded.", nameof(actors));
            this.instanceCount = 0; this.labelCount = 0;
            GeometryUtility.CalculateFrustumPlanes(camera, this.frustum);
            var playerPosition = snapshot.playerPosition;
            foreach (ref readonly var actor in actors) {
                if (actor.kind == ActorKind.Player) { playerPosition = actor.position; break; }
            }
            foreach (ref readonly var actor in actors) {
                var batch = this.visuals.SlotFor(actor.visualId);
                var bounds = this.visuals[batch].Geometry(actor, out var position, out var scale);
                if (!GeometryUtility.TestPlanesAABB(this.frustum, bounds)) continue;
                this.instances[this.instanceCount++] = new ActorRenderInstance(batch, Matrix4x4.TRS(position, Quaternion.identity, scale));
                if (actor.kind != ActorKind.Enemy || snapshot.isGameOver || this.labelCount == this.labels.Length ||
                    (actor.position - playerPosition).sqrMagnitude > this.labelDistanceSquared) continue;
                var screen = camera.WorldToScreenPoint(new Vector3(bounds.center.x, bounds.max.y + 0.3f, bounds.center.z));
                if (screen.z > 0f && screen.x >= 0f && screen.x <= screenSize.x && screen.y >= 0f && screen.y <= screenSize.y)
                    this.labels[this.labelCount++] = new EnemyHealthLabel(actor.id, screen, actor.health);
            }
        }
    }
}

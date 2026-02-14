using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Rendering {
    // Owns Unity render resources and bounded instancing batches, independent of ECS and HP.
    internal sealed class ActorBatchRenderer : IDisposable {
        private readonly InstancedRenderResources[] resources;
        private readonly InstancedBatchWriter[] writers;
        private bool disposed;
        public ActorBatchRenderer(Camera camera, ActorVisualCatalog visuals) {
            this.resources = new InstancedRenderResources[visuals.Count];
            this.writers = new InstancedBatchWriter[visuals.Count];
            try {
                for (var i = 0; i < this.resources.Length; i++) {
                    this.resources[i] = InstancedRenderHelper.CreateResources(visuals[i]);
                    this.writers[i] = InstancedRenderHelper.CreateBatchWriter(this.resources[i], new Matrix4x4[1023], camera);
                }
            } catch { this.Dispose(); throw; }
        }
        public void Draw(ReadOnlySpan<ActorRenderInstance> instances) {
            if (this.disposed) throw new ObjectDisposedException(nameof(ActorBatchRenderer));
            foreach (ref readonly var instance in instances) this.writers[instance.Batch].Add(instance.Matrix);
            for (var i = 0; i < this.writers.Length; i++) this.writers[i].Flush();
        }
        public void Dispose() {
            if (this.disposed) return;
            this.disposed = true;
            for (var i = 0; i < this.resources.Length; i++) InstancedRenderHelper.Dispose(ref this.resources[i]);
        }
    }
}

using System;
using Game.Config;
using Game.Domain;
using Game.Domain.Abilities;
using Game.UI;
using UnityEngine;
using Unity.Profiling;

namespace Game.Rendering {
    // Coordinates presentation from copied values. Owns frame preparation, camera and effects;
    // the batch renderer owns GPU resources, while the scene owns the HP view.
    public sealed class ArenaPresenter : IDisposable {
        private static readonly ProfilerMarker FrameMarker = new ProfilerMarker("Arena.Frame.Build");
        private static readonly ProfilerMarker DrawMarker = new ProfilerMarker("Arena.Actors.Draw");
        private static readonly ProfilerMarker HpMarker = new ProfilerMarker("Arena.HP.Apply");
        private static readonly ProfilerMarker CuesMarker = new ProfilerMarker("Arena.Cues.Render");
        private readonly Camera camera;
        private readonly EnemyHealthUiPresenter healthUi;
        private readonly ArenaCameraController cameraController;
        private readonly ActorFrameBuilder frameBuilder;
        private ActorBatchRenderer renderer;
        private AbilityCuePresenter abilityCues;
        private bool disposed;

        public ArenaPresenter(GameConfig config, Camera camera, Transform root,
            EnemyHealthUiPresenter healthUi, int viewCapacity, int abilityEventCapacity) {
            this.camera = camera; this.healthUi = healthUi;
            var labelCapacity = Math.Max(0, Math.Min(config.enemyCount, config.enemyWorldHpMaxLabels));
            this.cameraController = new ArenaCameraController(config, camera);
            var visuals = new ActorVisualCatalog(config);
            this.frameBuilder = new ActorFrameBuilder(config, viewCapacity, labelCapacity, visuals);
            try {
                this.healthUi.Prewarm(labelCapacity);
                this.renderer = new ActorBatchRenderer(camera, visuals);
                this.abilityCues = new AbilityCuePresenter(root, abilityEventCapacity,
                    config.playerAbility != null ? config.playerAbility.id : BuiltInAbilities.ShockwaveId, config.AbilityRingRadius);
            } catch { this.Dispose(); throw; }
        }
        public void Render(ReadOnlySpan<ActorView> actors, in BattleSnapshot snapshot, ReadOnlySpan<AbilityCastEvent> events, float deltaTime) {
            if (this.disposed) throw new ObjectDisposedException(nameof(ArenaPresenter));
            this.cameraController.Update(snapshot.phase, deltaTime);
            using (FrameMarker.Auto()) this.frameBuilder.Build(actors, snapshot, this.camera, new Vector2(Screen.width, Screen.height));
            using (DrawMarker.Auto()) this.renderer.Draw(this.frameBuilder.Instances);
            using (HpMarker.Auto()) this.healthUi.Apply(this.frameBuilder.Labels);
            using (CuesMarker.Auto()) this.abilityCues.Render(events, deltaTime, !snapshot.isGameOver);
        }
        public void Dispose() {
            if (this.disposed) return;
            this.disposed = true;
            this.healthUi?.ClearAll();
            this.renderer?.Dispose();
            this.abilityCues?.Dispose();
        }
    }
}

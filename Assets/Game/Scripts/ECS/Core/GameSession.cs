using System;
using System.Collections.Generic;
using Game.Domain.Abilities;
using Game.Domain;
using Game.Input;
using Scellecs.Morpeh;
using UnityEngine;
using Unity.Profiling;

namespace Game.ECS.Core {
    // Main-thread API. Owns the world and all simulation memory, never scene resources.
    public sealed class GameSession : IDisposable, Game.Domain.Abilities.IAbilityCommands {
        private static readonly ProfilerMarker SimulationMarker = new ProfilerMarker("Arena.Simulation.Step");
        private static readonly ProfilerMarker CopyMarker = new ProfilerMarker("Arena.Interpolation.Copy");
        public const float FixedStep = 1f / 60f;
        internal const int MaxStepsPerFrame = SimulationLimits.MaxStepsPerFrame;
        private readonly World world;
        private readonly GameContext context;
        private readonly SessionReader reader;
        private readonly SessionRenderFrames renderFrames;
        private readonly string sessionId = Guid.NewGuid().ToString("N");
        private bool disposed, hasResult;
        private BattleResult result;
        private readonly FixedStepClock clock = new FixedStepClock(FixedStep, MaxStepsPerFrame);

        public BattleSnapshot Snapshot { get; private set; }
        public Vector3 PlayerPosition => this.Snapshot.playerPosition;
        public int AbilityEventCapacity => this.context.AbilityEvents.Capacity;
        public int ViewCapacity => this.context.Config.ViewCapacity;
        public int PeakDamageRequests => this.context.DamageRequests.PeakCount;
        public double DroppedSimulationSeconds => this.clock.DroppedSeconds;

        public GameSession(SimulationSettings config, DamagePipeline damagePipeline = null, bool captureRenderFrames = false)
            : this(SessionConstruction.Complete(config, damagePipeline, captureRenderFrames)) { }

        private GameSession(SessionConstruction creation) {
            using (creation) {
                this.context = creation.Context; this.world = creation.World; this.reader = creation.Reader;
                this.renderFrames = creation.RenderFrames;
                this.RefreshSnapshot();
                this.renderFrames?.Capture(this.reader, true);
                creation.Transfer();
            }
        }

        // Main-thread operation. Dispose the iterator to cancel; no partial session escapes.
        // Successful callback return transfers the ready session to the caller.
        public static IEnumerator<object> CreateSteps(SimulationSettings config, Action<GameSession> ready,
            DamagePipeline damagePipeline = null, bool captureRenderFrames = false) {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (ready == null) throw new ArgumentNullException(nameof(ready));
            return CreateStepsCore(config, ready, damagePipeline, captureRenderFrames);
        }
        private static IEnumerator<object> CreateStepsCore(SimulationSettings config, Action<GameSession> ready,
            DamagePipeline damagePipeline, bool captureRenderFrames) {
            using var creation = new SessionConstruction(config, damagePipeline, captureRenderFrames);
            while (creation.MoveNext()) yield return null;
            var session = new GameSession(creation);
            try { ready(session); }
            catch { session.Dispose(); throw; }
        }

        // Press edges survive frames without a step; catch-up consumes an edge only once.
        // Overload is bounded and dropped time is exposed for diagnostics.
        public void TickUpdate(float elapsedSeconds, in PlayerInputFrame input) {
            this.ThrowIfDisposed();
            FixedStepClock.ValidateElapsed(elapsedSeconds);
            this.context.InputState.Submit(input);
            this.context.AbilityEvents.Clear();
            this.clock.BeginFrame(elapsedSeconds);
            while (this.clock.HasStep) {
                var previousPhase = this.Snapshot.phase;
                using (SimulationMarker.Auto()) this.world.Update(FixedStep);
                this.context.InputState.CompleteStep();
                this.clock.CompleteStep();
                this.RefreshSnapshot();
                this.renderFrames?.Capture(this.reader, previousPhase != this.Snapshot.phase);
            }
            this.clock.EndFrame();
        }

        // Equipment changes use stable actor IDs from CopyActors, never ECS entity indices.
        // The weapon must be registered in SimulationSettings.Weapons before session creation.
        public bool TryEquipWeapon(int actorId, int weaponId) {
            this.ThrowIfDisposed();
            return (this.Snapshot.phase == SessionPhase.AwaitingEntry || this.Snapshot.phase == SessionPhase.Combat)
                && this.reader.TryFindActor(actorId, out var actor)
                && Weapons.WeaponEquipment.TryChange(this.world, actor, this.context.Weapons, weaponId);
        }

        public bool TryActivateAbility(int slot) {
            this.ThrowIfDisposed();
            return this.Snapshot.phase == SessionPhase.Combat
                && Abilities.AbilityEquipment.Request(this.world, this.context.EntityLookup.player, slot);
        }

        public bool TryRequestAbility(int actorId, int slot) {
            this.ThrowIfDisposed();
            return this.Snapshot.phase == SessionPhase.Combat && this.reader.TryFindActor(actorId, out var actor)
                && Abilities.AbilityEquipment.Request(this.world, actor, slot);
        }

        private void RefreshSnapshot() {
            this.Snapshot = this.reader.ReadSnapshot();
            if (!this.hasResult && this.Snapshot.phase == SessionPhase.Results) {
                this.result = this.reader.ReadResult(this.sessionId, this.context.Config.victoryReward);
                this.hasResult = true;
            }
        }

        public bool TryGetResult(out BattleResult result) {
            this.ThrowIfDisposed();
            result = this.result;
            return this.hasResult && this.Snapshot.phase == SessionPhase.Meta;
        }

        // Caller owns destination. No entity, stash, filter, or native view escapes the world.
        public int CopyActors(Span<ActorView> destination) {
            this.ThrowIfDisposed();
            if (destination.Length < this.ViewCapacity) throw new ArgumentException("Allocate ViewCapacity elements before copying.", nameof(destination));
            return this.reader.CopyActors(destination);
        }

        // Presentation only: interpolate the final two fixed steps, with at most one step of
        // visual latency. New actors and phase transitions snap; removed actors disappear now.
        // Headless callers can leave captureRenderFrames disabled and get authoritative values.
        public int CopyRenderActors(Span<ActorView> destination) {
            using var sample = CopyMarker.Auto();
            this.ThrowIfDisposed();
            if (destination.Length < this.ViewCapacity) throw new ArgumentException("Allocate ViewCapacity elements before copying.", nameof(destination));
            if (this.renderFrames == null) return this.reader.CopyActors(destination);
            var alpha = this.Snapshot.isGameOver ? 1f : this.clock.InterpolationAlpha;
            return this.renderFrames.CopyTo(destination, alpha);
        }

        // Events from the latest host update. Copy is non-destructive for multiple readers;
        // the next TickUpdate invalidates this batch. Sequence identifies duplicates.
        public int CopyAbilityEvents(Span<AbilityCastEvent> destination) {
            this.ThrowIfDisposed();
            if (destination.Length < this.AbilityEventCapacity) throw new ArgumentException("Allocate AbilityEventCapacity elements.", nameof(destination));
            this.context.AbilityEvents.Items.CopyTo(destination);
            return this.context.AbilityEvents.Count;
        }

        public void Dispose() {
            if (this.disposed) return;
            this.disposed = true;
            try { this.world?.Dispose(); }
            finally { this.context?.Dispose(); }
        }
        private void ThrowIfDisposed() {
            if (this.disposed) throw new ObjectDisposedException(nameof(GameSession));
        }
    }
}

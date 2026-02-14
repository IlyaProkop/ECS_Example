using System;
using Game.Spatial;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.ECS.Movement {
    // Owns only job transport buffers and synchronous execution. No ECS access,
    // path decisions or handle can escape this boundary between simulation steps.
    internal sealed class EnemyMotionJobRunner : IDisposable {
        private static readonly ProfilerMarker PrepareMarker = new ProfilerMarker("Arena.Motion.Prepare");
        private static readonly ProfilerMarker SnapshotMarker = new ProfilerMarker("Arena.Motion.Snapshot");
        private static readonly ProfilerMarker SolveMarker = new ProfilerMarker("Arena.Motion.Solve");
        private static readonly ProfilerMarker ScheduleMarker = new ProfilerMarker("Arena.Motion.Schedule");
        private static readonly ProfilerMarker CompleteMarker = new ProfilerMarker("Arena.Motion.Complete");
        private static readonly ProfilerMarker CopyResultsMarker = new ProfilerMarker("Arena.Motion.CopyResults");
        private NativeArray<EnemyMotionInput> inputs;
        private NativeArray<EnemyMotionResult> results;

        public EnemyMotionJobRunner(int capacity) {
            try {
                this.inputs = new NativeArray<EnemyMotionInput>(capacity, Allocator.Persistent);
                this.results = new NativeArray<EnemyMotionResult>(capacity, Allocator.Persistent);
            } catch { this.Dispose(); throw; }
        }

        public void Solve(EnemyMotionInput[] source, EnemyMotionResult[] destination, int count,
            in EnemyMotionFrame frame, NativeEnemyGeometrySnapshot.ReadView neighbors,
            float2 arenaHalfSize, in ObstacleGridLookupNative obstacles) {
            using (PrepareMarker.Auto()) {
                using var snapshotSample = SnapshotMarker.Auto();
                NativeArray<EnemyMotionInput>.Copy(source, this.inputs, count);
            }
            using (SolveMarker.Auto()) {
                JobHandle handle;
                using (ScheduleMarker.Auto()) handle = new EnemyMotionJob {
                    Inputs = this.inputs, Results = this.results, Neighbors = neighbors,
                    Obstacles = obstacles, Frame = frame, ArenaHalfSize = arenaHalfSize
                }.Schedule(count, 64);
                using (CompleteMarker.Auto()) handle.Complete();
            }
            using (CopyResultsMarker.Auto()) NativeArray<EnemyMotionResult>.Copy(this.results, destination, count);
        }

        public void Dispose() {
            if (this.results.IsCreated) this.results.Dispose();
            if (this.inputs.IsCreated) this.inputs.Dispose();
        }
    }
}

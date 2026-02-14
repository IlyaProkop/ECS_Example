using Game.Spatial;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;

namespace Game.ECS.Movement {
    [BurstCompile(FloatMode = FloatMode.Strict, FloatPrecision = FloatPrecision.High, CompileSynchronously = true)]
    internal struct EnemyMotionJob : IJobParallelFor {
        [ReadOnly] public NativeArray<EnemyMotionInput> Inputs;
        [WriteOnly] public NativeArray<EnemyMotionResult> Results;
        public NativeEnemyGeometrySnapshot.ReadView Neighbors;
        public ObstacleGridLookupNative Obstacles;
        public EnemyMotionFrame Frame;
        public float2 ArenaHalfSize;
        public void Execute(int index) {
            var input = this.Inputs[index];
            this.Results[index] = EnemyMotionSolver.SolveOne(input, this.Frame,
                this.Neighbors.Query(input.Position, EnemyMotionSolver.NeighborRange(input, this.Frame)),
                this.ArenaHalfSize, this.Obstacles);
        }
    }
}

using System;
using Game.Domain;
using Unity.Collections;
using UnityEngine;

namespace Game.Spatial {
    internal struct ObstacleBounds {
        public float minX;
        public float maxX;
        public float minZ;
        public float maxZ;
        // Preserve authoring centers exactly for strict abs(delta) collision tests.
        // Recovering a center from rounded min/max bounds can change edge classification.
        public Vector2 center;
    }

    internal struct ObstacleGridLookupNative {
        [ReadOnly] public NativeArray<ObstacleBounds> obstacleBounds;
        [ReadOnly] public NativeArray<int> cellStarts;
        [ReadOnly] public NativeArray<int> cellCounts;
        [ReadOnly] public NativeArray<int> obstacleIndices;
        public int width;
        public int height;
        public float originX;
        public float originZ;
        public float cellSize;
        public float inverseCellSize;
        public float obstacleHalfX;
        public float obstacleHalfZ;
    }

    internal sealed class ObstacleGridLookup : IDisposable {
        private NativeArray<ObstacleBounds> obstacleBounds;
        private NativeArray<int> cellStarts;
        private NativeArray<int> cellCounts;
        private NativeArray<int> obstacleIndices;

        private int width;
        private int height;
        private float originX;
        private float originZ;
        private float cellSize;
        private float inverseCellSize;
        private float obstacleHalfX;
        private float obstacleHalfZ;

        public ObstacleGridLookup(SimulationSettings config, float maxAgentRadius) {
            try { this.Build(config, maxAgentRadius); }
            catch { this.Dispose(); throw; }
        }

        public bool HasObstacles => this.obstacleBounds.IsCreated && this.obstacleBounds.Length > 0;

        public ObstacleGridLookupNative AsNative() {
            return new ObstacleGridLookupNative {
                obstacleBounds = this.obstacleBounds,
                cellStarts = this.cellStarts,
                cellCounts = this.cellCounts,
                obstacleIndices = this.obstacleIndices,
                width = this.width,
                height = this.height,
                originX = this.originX,
                originZ = this.originZ,
                cellSize = this.cellSize,
                inverseCellSize = this.inverseCellSize,
                obstacleHalfX = this.obstacleHalfX,
                obstacleHalfZ = this.obstacleHalfZ
            };
        }

        public void Dispose() {
            if (this.obstacleBounds.IsCreated) {
                this.obstacleBounds.Dispose();
            }

            if (this.cellStarts.IsCreated) {
                this.cellStarts.Dispose();
            }

            if (this.cellCounts.IsCreated) {
                this.cellCounts.Dispose();
            }

            if (this.obstacleIndices.IsCreated) {
                this.obstacleIndices.Dispose();
            }
        }

        private void Build(SimulationSettings config, float maxAgentRadius) {
            var obstacles = config.obstaclePositions;
            var obstacleCount = obstacles.Count;

            this.cellSize = Mathf.Max(0.25f, config.obstacleLookupCellSize);
            this.inverseCellSize = 1f / this.cellSize;
            this.obstacleHalfX = Mathf.Abs(config.obstacleScale.x) * 0.5f;
            this.obstacleHalfZ = Mathf.Abs(config.obstacleScale.z) * 0.5f;
            maxAgentRadius = Mathf.Max(0f, maxAgentRadius);

            if (obstacleCount <= 0) {
                this.width = 1;
                this.height = 1;
                this.originX = -this.cellSize * 0.5f;
                this.originZ = -this.cellSize * 0.5f;
                this.obstacleBounds = new NativeArray<ObstacleBounds>(0, Allocator.Persistent);
                this.cellStarts = new NativeArray<int>(1, Allocator.Persistent);
                this.cellCounts = new NativeArray<int>(1, Allocator.Persistent);
                this.obstacleIndices = new NativeArray<int>(0, Allocator.Persistent);
                return;
            }

            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var minZ = float.PositiveInfinity;
            var maxZ = float.NegativeInfinity;

            for (var i = 0; i < obstacleCount; i++) {
                var obstacle = obstacles[i];
                minX = Mathf.Min(minX, obstacle.x);
                maxX = Mathf.Max(maxX, obstacle.x);
                minZ = Mathf.Min(minZ, obstacle.z);
                maxZ = Mathf.Max(maxZ, obstacle.z);
            }

            var paddingX = this.obstacleHalfX + maxAgentRadius + this.cellSize;
            var paddingZ = this.obstacleHalfZ + maxAgentRadius + this.cellSize;
            minX -= paddingX;
            maxX += paddingX;
            minZ -= paddingZ;
            maxZ += paddingZ;

            this.width = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) * this.inverseCellSize) + 1);
            this.height = Mathf.Max(1, Mathf.CeilToInt((maxZ - minZ) * this.inverseCellSize) + 1);
            this.originX = minX;
            this.originZ = minZ;

            var cellCount = this.width * this.height;
            var cellCountManaged = new int[cellCount];
            var cellStartManaged = new int[cellCount];
            var cellOffsetManaged = new int[cellCount];
            var obstacleIndexManaged = new int[obstacleCount];
            var obstacleBoundsManaged = new ObstacleBounds[obstacleCount];

            for (var i = 0; i < obstacleCount; i++) {
                var obstacle = obstacles[i];
                var cellIndex = GetCellIndex(
                    obstacle.x,
                    obstacle.z,
                    this.width,
                    this.height,
                    this.originX,
                    this.originZ,
                    this.inverseCellSize);
                cellCountManaged[cellIndex]++;
            }

            var running = 0;
            for (var i = 0; i < cellCount; i++) {
                cellStartManaged[i] = running;
                cellOffsetManaged[i] = running;
                running += cellCountManaged[i];
            }

            for (var i = 0; i < obstacleCount; i++) {
                var obstacle = obstacles[i];
                obstacleBoundsManaged[i] = new ObstacleBounds {
                    minX = obstacle.x - this.obstacleHalfX,
                    maxX = obstacle.x + this.obstacleHalfX,
                    minZ = obstacle.z - this.obstacleHalfZ,
                    maxZ = obstacle.z + this.obstacleHalfZ,
                    center = new Vector2(obstacle.x, obstacle.z)
                };

                var cellIndex = GetCellIndex(
                    obstacle.x,
                    obstacle.z,
                    this.width,
                    this.height,
                    this.originX,
                    this.originZ,
                    this.inverseCellSize);
                obstacleIndexManaged[cellOffsetManaged[cellIndex]++] = i;
            }

            this.obstacleBounds = new NativeArray<ObstacleBounds>(obstacleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            this.cellStarts = new NativeArray<int>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            this.cellCounts = new NativeArray<int>(cellCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            this.obstacleIndices = new NativeArray<int>(obstacleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (var i = 0; i < obstacleCount; i++) {
                this.obstacleBounds[i] = obstacleBoundsManaged[i];
                this.obstacleIndices[i] = obstacleIndexManaged[i];
            }

            for (var i = 0; i < cellCount; i++) {
                this.cellStarts[i] = cellStartManaged[i];
                this.cellCounts[i] = cellCountManaged[i];
            }
        }

        private static int GetCellIndex(float x, float z, int width, int height, float originX, float originZ, float inverseCellSize) {
            var cellX = Mathf.Clamp(Mathf.FloorToInt((x - originX) * inverseCellSize), 0, width - 1);
            var cellZ = Mathf.Clamp(Mathf.FloorToInt((z - originZ) * inverseCellSize), 0, height - 1);
            return cellZ * width + cellX;
        }
    }
}

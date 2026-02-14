using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Spatial;
using UnityEngine;

namespace Game.ECS.Navigation {
    internal sealed class AStarGridPathfinder {
        private const float DiagonalMoveCost = 1.41421356f;

        private const uint Blocked = 1, Closed = 2, Visited = 4, FlagMask = 7;
        private readonly PathOpenSet openSet;
        private readonly List<int> rawPath;
        private readonly uint[] nodeStates;
        private readonly float[] gScores;
        private readonly int[] parents;
        private uint generation;
        private readonly float cellSize, searchPadding, maxSearchSize, obstacleHalfX, obstacleHalfZ;
        private readonly int maxNodes;
        private readonly Vector2 arenaHalfSize;
        private readonly ObstacleGridLookup obstacles;

        // Search policy and immutable map belong to the session. Node state is reusable workspace;
        // the caller owns the result list, preallocated to the configured node budget.
        public AStarGridPathfinder(NavigationSettings config, ArenaSettings arena, ObstacleGridLookup obstacles) {
            if (config == null) throw new ArgumentNullException(nameof(config));
            this.obstacles = obstacles ?? throw new ArgumentNullException(nameof(obstacles));
            this.cellSize = Mathf.Max(0.05f, config.enemyPathCellSize);
            this.searchPadding = Mathf.Max(0.1f, config.enemyPathSearchPadding);
            this.maxSearchSize = Mathf.Max(2f, config.enemyPathMaxSearchSize);
            this.maxNodes = config.enemyPathMaxNodes;
            this.arenaHalfSize = arena.HalfSize;
            this.obstacleHalfX = arena.ObstacleScale.x * 0.5f;
            this.obstacleHalfZ = arena.ObstacleScale.z * 0.5f;
            this.nodeStates = new uint[this.maxNodes];
            this.gScores = new float[this.maxNodes];
            this.parents = new int[this.maxNodes];
            this.openSet = new PathOpenSet(this.maxNodes);
            this.rawPath = new List<int>(this.maxNodes);
        }

        public bool TryFindPath(Vector3 startWorld, Vector3 goalWorld, float agentRadius, List<Vector3> result) {
            result.Clear();
            var extent = this.arenaHalfSize - Vector2.one * agentRadius;
            if (extent.x < 0f || extent.y < 0f || !InsideArena(startWorld, extent) || !InsideArena(goalWorld, extent)) return false;
            var lookup = this.obstacles.AsNative();
            if (!ObstacleQueries.HasLineOfSight(startWorld, startWorld, agentRadius, lookup) ||
                !ObstacleQueries.HasLineOfSight(goalWorld, goalWorld, agentRadius, lookup)) return false;
            var cellSize = this.cellSize;
            var searchPadding = this.searchPadding;

            var spanX = Mathf.Abs(goalWorld.x - startWorld.x) + searchPadding * 2f;
            var spanZ = Mathf.Abs(goalWorld.z - startWorld.z) + searchPadding * 2f;
            if (spanX > this.maxSearchSize || spanZ > this.maxSearchSize) {
                return false;
            }

            var minX = Mathf.Min(startWorld.x, goalWorld.x) - searchPadding;
            var maxX = Mathf.Max(startWorld.x, goalWorld.x) + searchPadding;
            var minZ = Mathf.Min(startWorld.z, goalWorld.z) - searchPadding;
            var maxZ = Mathf.Max(startWorld.z, goalWorld.z) + searchPadding;

            var width = Mathf.Max(2, Mathf.CeilToInt((maxX - minX) / cellSize) + 1);
            var height = Mathf.Max(2, Mathf.CeilToInt((maxZ - minZ) / cellSize) + 1);
            if ((long)width * height > this.maxNodes) {
                return false;
            }

            this.BeginSearch();
            this.MarkBlockedNodes(agentRadius, minX, minZ, cellSize, width, height);
            for (var z = 0; z < height; z++) {
                for (var x = 0; x < width; x++) {
                    if (!InsideArena(new Vector3(minX + x * cellSize, 0f, minZ + z * cellSize), extent))
                        this.nodeStates[CellToIndex(x, z, width)] = this.generation | Blocked;
                }
            }

            var startX = WorldToCell(startWorld.x, minX, cellSize, width);
            var startZ = WorldToCell(startWorld.z, minZ, cellSize, height);
            var goalX = WorldToCell(goalWorld.x, minX, cellSize, width);
            var goalZ = WorldToCell(goalWorld.z, minZ, cellSize, height);

            var startNode = CellToIndex(startX, startZ, width);
            var goalNode = CellToIndex(goalX, goalZ, width);

            // Never turn an inaccessible grid cell into a traversable endpoint.
            if (this.IsBlocked(startNode) || this.IsBlocked(goalNode)) return false;
            var startAnchor = new Vector3(minX + startX * cellSize, startWorld.y, minZ + startZ * cellSize);
            var goalAnchor = new Vector3(minX + goalX * cellSize, startWorld.y, minZ + goalZ * cellSize);
            if (!ObstacleQueries.HasLineOfSight(startWorld, startAnchor, agentRadius, lookup) ||
                !ObstacleQueries.HasLineOfSight(goalAnchor, goalWorld, agentRadius, lookup)) return false;
            this.nodeStates[startNode] = this.generation | Visited;
            this.gScores[startNode] = 0f;
            this.openSet.AddOrDecrease(startNode, EstimateCost(startX, startZ, goalX, goalZ));

            while (this.openSet.Count > 0) {
                var current = this.openSet.Pop();
                if (current == goalNode) {
                    this.BuildSimplifiedPath(goalNode, startNode, width, minX, minZ, cellSize, startWorld.y, result);
                    if (result.Count == 0) return false;
                    if ((result[0] - startWorld).sqrMagnitude > 0.000001f) result.Insert(0, startWorld);
                    if ((result[result.Count - 1] - goalWorld).sqrMagnitude > 0.000001f) result.Add(goalWorld);
                    return true;
                }

                this.nodeStates[current] |= Closed;

                var currentX = current % width;
                var currentZ = current / width;
                for (var zOffset = -1; zOffset <= 1; zOffset++) {
                    for (var xOffset = -1; xOffset <= 1; xOffset++) {
                        if (xOffset == 0 && zOffset == 0) {
                            continue;
                        }

                        var nextX = currentX + xOffset;
                        var nextZ = currentZ + zOffset;
                        if (nextX < 0 || nextX >= width || nextZ < 0 || nextZ >= height) {
                            continue;
                        }

                        var nextNode = CellToIndex(nextX, nextZ, width);
                        var state = this.nodeStates[nextNode];
                        if ((state & ~FlagMask) != this.generation) state = 0;
                        if ((state & (Blocked | Closed)) != 0) {
                            continue;
                        }

                        if (xOffset != 0 && zOffset != 0) {
                            var sideA = CellToIndex(currentX + xOffset, currentZ, width);
                            var sideB = CellToIndex(currentX, currentZ + zOffset, width);
                            if (this.IsBlocked(sideA) || this.IsBlocked(sideB)) {
                                continue;
                            }
                        }

                        var stepCost = xOffset == 0 || zOffset == 0 ? 1f : DiagonalMoveCost;
                        var candidateG = this.gScores[current] + stepCost;

                        if ((state & Visited) != 0 && candidateG >= this.gScores[nextNode]) {
                            continue;
                        }

                        this.parents[nextNode] = current;
                        this.gScores[nextNode] = candidateG;
                        this.nodeStates[nextNode] = this.generation | Visited;
                        this.openSet.AddOrDecrease(nextNode, candidateG + EstimateCost(nextX, nextZ, goalX, goalZ));
                    }
                }
            }

            return false;
        }

        private static bool InsideArena(Vector3 point, Vector2 extent) =>
            Mathf.Abs(point.x) <= extent.x && Mathf.Abs(point.z) <= extent.y;

        private static int WorldToCell(float world, float min, float cellSize, int size) {
            return Mathf.Clamp(Mathf.RoundToInt((world - min) / cellSize), 0, size - 1);
        }

        private static int CellToIndex(int x, int z, int width) {
            return z * width + x;
        }

        private static float EstimateCost(int x, int z, int targetX, int targetZ) {
            var dx = Mathf.Abs(targetX - x);
            var dz = Mathf.Abs(targetZ - z);
            var diagonal = Mathf.Min(dx, dz);
            var straight = Mathf.Max(dx, dz) - diagonal;
            return diagonal * DiagonalMoveCost + straight;
        }

        private void BeginSearch() {
            this.openSet.Clear();
            this.rawPath.Clear();
            // Low three bits are node flags; high bits identify this search's local grid.
            this.generation = unchecked(this.generation + FlagMask + 1);
            if (this.generation != 0) return;
            Array.Clear(this.nodeStates, 0, this.nodeStates.Length);
            this.generation = FlagMask + 1;
        }

        private bool IsBlocked(int node) => this.nodeStates[node] == (this.generation | Blocked);

        private void MarkBlockedNodes(float agentRadius, float minX, float minZ, float cellSize, int width, int height) {
            var halfX = this.obstacleHalfX + agentRadius;
            var halfZ = this.obstacleHalfZ + agentRadius;
            var lookup = this.obstacles.AsNative();

            // Rasterization expands via floor/ceil, so include one extra navigation cell
            // beyond the grid edges. Keep the original center +/- (half + radius) arithmetic.
            foreach (var i in ObstacleQueries.QueryBounds(lookup, minX, minX + (width - 1) * cellSize,
                minZ, minZ + (height - 1) * cellSize, agentRadius + cellSize)) {
                var obstacle = lookup.obstacleBounds[i].center;
                var obstacleMinX = obstacle.x - halfX;
                var obstacleMaxX = obstacle.x + halfX;
                var obstacleMinZ = obstacle.y - halfZ;
                var obstacleMaxZ = obstacle.y + halfZ;

                var minCellX = Mathf.Max(0, Mathf.FloorToInt((obstacleMinX - minX) / cellSize));
                var maxCellX = Mathf.Min(width - 1, Mathf.CeilToInt((obstacleMaxX - minX) / cellSize));
                var minCellZ = Mathf.Max(0, Mathf.FloorToInt((obstacleMinZ - minZ) / cellSize));
                var maxCellZ = Mathf.Min(height - 1, Mathf.CeilToInt((obstacleMaxZ - minZ) / cellSize));

                if (minCellX > maxCellX || minCellZ > maxCellZ) {
                    continue;
                }

                for (var z = minCellZ; z <= maxCellZ; z++) {
                    for (var x = minCellX; x <= maxCellX; x++) {
                        this.nodeStates[CellToIndex(x, z, width)] = this.generation | Blocked;
                    }
                }
            }
        }

        private void BuildSimplifiedPath(
            int goalNode,
            int startNode,
            int width,
            float minX,
            float minZ,
            float cellSize,
            float worldY,
            List<Vector3> result) {
            this.rawPath.Clear();
            var current = goalNode;
            this.rawPath.Add(current);

            while (current != startNode) {
                current = this.parents[current];
                if (current < 0) {
                    this.rawPath.Clear();
                    result.Clear();
                    return;
                }

                this.rawPath.Add(current);
            }

            if (this.rawPath.Count == 0) {
                result.Clear();
                return;
            }

            result.Clear();

            var startPathNode = this.rawPath[this.rawPath.Count - 1];
            AddWorldPoint(startPathNode, width, minX, minZ, cellSize, worldY, result);

            var previousNode = startPathNode;
            var hasPreviousDirection = false;
            var previousDirectionX = 0;
            var previousDirectionZ = 0;

            for (var i = this.rawPath.Count - 2; i >= 0; i--) {
                var nextNode = this.rawPath[i];

                var previousX = previousNode % width;
                var previousZ = previousNode / width;
                var nextX = nextNode % width;
                var nextZ = nextNode / width;

                var directionX = nextX - previousX;
                var directionZ = nextZ - previousZ;

                if (hasPreviousDirection &&
                    (directionX != previousDirectionX || directionZ != previousDirectionZ)) {
                    AddWorldPoint(previousNode, width, minX, minZ, cellSize, worldY, result);
                }

                previousDirectionX = directionX;
                previousDirectionZ = directionZ;
                hasPreviousDirection = true;
                previousNode = nextNode;
            }

            AddWorldPoint(previousNode, width, minX, minZ, cellSize, worldY, result);
        }

        private static void AddWorldPoint(
            int node,
            int width,
            float minX,
            float minZ,
            float cellSize,
            float worldY,
            List<Vector3> result) {
            var x = node % width;
            var z = node / width;
            var point = new Vector3(minX + x * cellSize, worldY, minZ + z * cellSize);

            if (result.Count > 0 && (result[result.Count - 1] - point).sqrMagnitude <= 0.000001f) {
                return;
            }

            result.Add(point);
        }
    }
}

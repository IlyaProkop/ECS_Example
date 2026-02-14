using System.Collections.Generic;
using System.Reflection;
using Game.Config;
using Game.ECS.Navigation;
using Game.Spatial;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class PathfinderWorkspaceTests {
        private GameConfig authoring;

        [SetUp]
        public void SetUp() {
            this.authoring = ScriptableObject.CreateInstance<GameConfig>();
            this.authoring.obstaclePositions = new[] { Vector3.zero };
            this.authoring.obstacleScale = Vector3.one * 2f;
            this.authoring.enemyPathCellSize = 0.5f;
            this.authoring.enemyPathSearchPadding = 3f;
            this.authoring.enemyPathMaxSearchSize = 40f;
            this.authoring.enemyPathMaxNodes = 4096;
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(this.authoring);

        [Test]
        public void ReusedWorkspaceMatchesFreshSearchAfterChangingGridOriginDimensionsAndRadius() {
            var positions = new List<Vector3>();
            for (var z = -7; z <= 7; z++) for (var x = -7; x <= 7; x++) positions.Add(new Vector3(x * 4f, 0f, z * 4f));
            this.authoring.obstaclePositions = positions.ToArray();
            var config = this.authoring.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(config, 1f);
            var reused = new AStarGridPathfinder(config.Navigation, config.Arena, obstacles);
            var actual = new List<Vector3>(config.enemyPathMaxNodes);
            var expected = new List<Vector3>(config.enemyPathMaxNodes);
            var random = new System.Random(2345);
            for (var i = 0; i < 80; i++) {
                var start = new Vector3(random.Next(-10, 10) * 0.5f, 0f, random.Next(-10, 10) * 0.5f);
                var end = new Vector3(random.Next(-15, 15) * 0.5f, 0f, random.Next(-15, 15) * 0.5f);
                var radius = i % 2 == 0 ? 0.5f : 1f;
                var fresh = new AStarGridPathfinder(config.Navigation, config.Arena, obstacles);
                Assert.That(reused.TryFindPath(start, end, radius, actual), Is.EqualTo(fresh.TryFindPath(start, end, radius, expected)), "Search " + i);
                Assert.That(actual, Is.EqualTo(expected), "Search " + i);
            }
        }

        [TestCase(0.5f)]
        [TestCase(1f)]
        public void DetourKeepsEndpointCoordinatesAndClearsInflatedObstacleOnEverySegment(float radius) {
            var config = this.authoring.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(config, 1f);
            var pathfinder = new AStarGridPathfinder(config.Navigation, config.Arena, obstacles);
            var path = new List<Vector3>(config.enemyPathMaxNodes);
            var start = Vector3.left * 5f;
            var goal = Vector3.right * 5f;
            Assert.That(pathfinder.TryFindPath(start, goal, radius, path), Is.True);
            Assert.That(path[0], Is.EqualTo(start));
            Assert.That(path[path.Count - 1], Is.EqualTo(goal));
            Assert.That(path.Exists(point => point.z < 0f), Is.True, "Stable tie order selects the lower detour.");
            for (var i = 1; i < path.Count; i++) {
                // Independent exact test of every simplified segment, without the candidate grid.
                Assert.That(Geometry2D.SegmentIntersectsAabb2D(new Vector2(path[i - 1].x, path[i - 1].z),
                    new Vector2(path[i].x, path[i].z), -1f - radius, 1f + radius, -1f - radius, 1f + radius), Is.False);
            }
        }

        [Test]
        public void FailedAndRejectedSearchesClearOutputAndDoNotPoisonNextRoute() {
            var config = this.authoring.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(config, 1f);
            var pathfinder = new AStarGridPathfinder(config.Navigation, config.Arena, obstacles);
            var path = new List<Vector3>(config.enemyPathMaxNodes);
            Assert.That(pathfinder.TryFindPath(Vector3.left * 5f, Vector3.right * 5f, 0.5f, path), Is.True);
            var expected = path.ToArray();
            Assert.That(pathfinder.TryFindPath(Vector3.zero, Vector3.right * 5f, 0.5f, path), Is.False, "Isolated start cannot cut blocked corners.");
            Assert.That(path, Is.Empty);
            Assert.That(pathfinder.TryFindPath(Vector3.left * 100f, Vector3.right * 100f, 0.5f, path), Is.False, "Search-size budget");
            Assert.That(path, Is.Empty);
            Assert.That(pathfinder.TryFindPath(Vector3.left * 5f, Vector3.right * 5f, 0.5f, path), Is.True);
            Assert.That(path, Is.EqualTo(expected));
        }

        [Test]
        public void WallAdjacentObstacleMustBeDetouredInsideArena() {
            this.authoring.obstaclePositions = new[] { new Vector3(0f, 0f, -8f) };
            this.authoring.obstacleScale = new Vector3(2f, 2f, 4f);
            this.authoring.enemyPathSearchPadding = 4f;
            var config = this.authoring.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(config, 1f);
            var path = new List<Vector3>();
            var start = new Vector3(-4f, 0f, -8f);
            var goal = new Vector3(4f, 0f, -8f);
            Assert.That(new AStarGridPathfinder(config.Navigation, config.Arena, obstacles).TryFindPath(start, goal, 0.5f, path), Is.True);
            AssertSafePath(path, start, goal, 0.5f, config.arenaHalfSize, obstacles);
            Assert.That(path.Exists(point => point.z > -5.5f), Is.True);
        }

        [Test]
        public void BarrierAcrossArenaCannotBeDetouredOutsideWalls() {
            this.authoring.arenaHalfSize = new Vector2(6f, 6f);
            this.authoring.obstacleScale = new Vector3(2f, 2f, 12f);
            this.authoring.enemyPathSearchPadding = 9f;
            var config = this.authoring.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(config, 1f);
            var path = new List<Vector3> { Vector3.one };
            Assert.That(new AStarGridPathfinder(config.Navigation, config.Arena, obstacles).TryFindPath(Vector3.left * 3f,
                Vector3.right * 3f, 0.5f, path), Is.False);
            Assert.That(path, Is.Empty);
        }

        [Test]
        public void GoalAccessibleToSmallActorIsRejectedForLargerActor() {
            var config = this.authoring.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(config, 1f);
            var goal = new Vector3(1.79f, 0f, 0.13f);
            Assert.That(ObstacleQueries.HasLineOfSight(goal, goal, 0.3f, obstacles.AsNative()), Is.True);
            var path = new List<Vector3>();
            Assert.That(new AStarGridPathfinder(config.Navigation, config.Arena, obstacles).TryFindPath(Vector3.left * 5.13f,
                goal, 0.8f, path), Is.False);
            Assert.That(path, Is.Empty);
        }

        [Test]
        public void OffGridEndpointsHaveSafeConnectionsToReturnedRoute() {
            var config = this.authoring.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(config, 1f);
            var start = new Vector3(-4.83f, 0f, 0.11f);
            var goal = new Vector3(4.71f, 0f, -0.17f);
            var path = new List<Vector3>();
            Assert.That(new AStarGridPathfinder(config.Navigation, config.Arena, obstacles).TryFindPath(start, goal, 0.5f, path), Is.True);
            AssertSafePath(path, start, goal, 0.5f, config.arenaHalfSize, obstacles);
        }

        [Test]
        public void EndpointInsideWallMarginIsRejected() {
            var config = this.authoring.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(config, 1f);
            var path = new List<Vector3>();
            Assert.That(new AStarGridPathfinder(config.Navigation, config.Arena, obstacles).TryFindPath(Vector3.right * 5f,
                new Vector3(config.arenaHalfSize.x - 0.1f, 0f, 0f), 0.5f, path), Is.False);
            Assert.That(path, Is.Empty);
        }

        private static void AssertSafePath(List<Vector3> path, Vector3 start, Vector3 goal,
            float radius, Vector2 extent, ObstacleGridLookup obstacles) {
            Assert.That(path[0], Is.EqualTo(start));
            Assert.That(path[path.Count - 1], Is.EqualTo(goal));
            for (var i = 0; i < path.Count; i++) {
                Assert.That(Mathf.Abs(path[i].x), Is.LessThanOrEqualTo(extent.x - radius));
                Assert.That(Mathf.Abs(path[i].z), Is.LessThanOrEqualTo(extent.y - radius));
                if (i > 0) Assert.That(ObstacleQueries.HasLineOfSight(path[i - 1], path[i], radius,
                    obstacles.AsNative()), Is.True, "Segment " + i);
            }
        }

        [Test]
        public void GenerationWrapCannotReuseOldBlockedVisitedOrParentState() {
            var config = this.authoring.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(config, 1f);
            var pathfinder = new AStarGridPathfinder(config.Navigation, config.Arena, obstacles);
            var path = new List<Vector3>(config.enemyPathMaxNodes);
            pathfinder.TryFindPath(Vector3.left * 5f, Vector3.right * 5f, 1f, path);
            typeof(AStarGridPathfinder).GetField("generation", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(pathfinder, uint.MaxValue - 7u);
            var expected = new List<Vector3>(config.enemyPathMaxNodes);
            var start = new Vector3(-3f, 0f, 2f);
            var goal = new Vector3(5f, 0f, -2f);
            var fresh = new AStarGridPathfinder(config.Navigation, config.Arena, obstacles);
            Assert.That(pathfinder.TryFindPath(start, goal, 0.5f, path), Is.EqualTo(fresh.TryFindPath(start, goal, 0.5f, expected)));
            Assert.That(path, Is.EqualTo(expected));
            Assert.That(pathfinder.TryFindPath(start, start, 0.5f, path), Is.True);
            Assert.That(path, Is.EqualTo(new[] { start }));
        }
    }
}

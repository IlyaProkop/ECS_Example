using System;
using System.Collections.Generic;
using Game.Config;
using Game.Spatial;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class ObstacleQueryTests {
        private GameConfig config;
        private ObstacleGridLookup obstacles;

        [SetUp]
        public void SetUp() {
            this.config = ScriptableObject.CreateInstance<GameConfig>();
            this.config.obstacleScale = new Vector3(1.5f, 1f, 2f);
            this.config.obstacleLookupCellSize = 2f;
            this.config.obstaclePositions = new Vector3[225];
            for (var z = 0; z < 15; z++) for (var x = 0; x < 15; x++)
                this.config.obstaclePositions[z * 15 + x] = new Vector3(x * 4f - 28f, 0f, z * 4f - 28f);
            this.obstacles = new ObstacleGridLookup(this.config.CreateSimulationSettings(), 1f);
        }
        [TearDown]
        public void TearDown() { this.obstacles.Dispose(); Object.DestroyImmediate(this.config); }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(3f)]
        public void SegmentQueriesMatchAllObstaclesForShortLongAndStationarySegments(float radius) {
            var random = new System.Random(7383);
            var lookup = this.obstacles.AsNative();
            for (var query = 0; query < 250; query++) {
                var start = new Vector2((float)random.NextDouble() * 80f - 40f, (float)random.NextDouble() * 80f - 40f);
                var end = query % 3 == 0 ? start : query % 3 == 1 ? start + new Vector2(0.3f, -0.2f) : -start;
                var expected = float.MaxValue;
                foreach (var obstacle in lookup.obstacleBounds) {
                    if (Geometry2D.SegmentIntersectsAabb2D(start, end, obstacle.minX - radius, obstacle.maxX + radius,
                        obstacle.minZ - radius, obstacle.maxZ + radius, out var t)) expected = Mathf.Min(expected, t);
                }
                var found = ObstacleQueries.TryFindFirstHit(start, end, radius, lookup, out var actual);
                Assert.That(found, Is.EqualTo(expected < float.MaxValue), "Query " + query);
                Assert.That(actual, Is.EqualTo(expected), "First hit parameter, query " + query);
                Assert.That(ObstacleQueries.HasLineOfSight(new Vector3(start.x, 0f, start.y), new Vector3(end.x, 0f, end.y), radius, lookup), Is.EqualTo(!found));
            }
        }

        [Test]
        public void CandidateRectangleIncludesEveryOverlapExactlyOnceAndCursorsAreIndependent() {
            var lookup = this.obstacles.AsNative();
            var query = ObstacleQueries.QueryBounds(lookup, -8f, 8f, -6f, 6f, 1f);
            var ids = new HashSet<int>();
            var first = query.GetEnumerator();
            var second = query.GetEnumerator();
            Assert.Throws<InvalidOperationException>(() => { var unused = first.Current; });
            Assert.That(first.MoveNext(), Is.True);
            Assert.That(second.MoveNext(), Is.True);
            Assert.That(first.Current, Is.EqualTo(second.Current));
            var secondValue = second.Current;
            first.MoveNext();
            Assert.That(second.Current, Is.EqualTo(secondValue));
            foreach (var index in query) Assert.That(ids.Add(index), Is.True, "Duplicate obstacle " + index);
            for (var i = 0; i < lookup.obstacleBounds.Length; i++) {
                var b = lookup.obstacleBounds[i];
                if (b.maxX + 1f >= -8f && b.minX - 1f <= 8f && b.maxZ + 1f >= -6f && b.minZ - 1f <= 6f)
                    Assert.That(ids.Contains(i), Is.True, "Missing overlapping obstacle " + i);
            }
            Assert.That(ids.Count, Is.LessThan(lookup.obstacleBounds.Length), "A local grid query should cull distant obstacles.");
        }

        [Test]
        public void TangencyAndStartingInsideKeepInclusiveSegmentCollisionRules() {
            var lookup = this.obstacles.AsNative();
            Assert.That(ObstacleQueries.TryFindFirstHit(new Vector2(-2f, 1f), new Vector2(2f, 1f), 0f, lookup, out var tangent), Is.True);
            Assert.That(tangent, Is.EqualTo(0.3125f));
            Assert.That(ObstacleQueries.TryFindFirstHit(Vector2.zero, Vector2.zero, 0f, lookup, out var inside), Is.True);
            Assert.That(inside, Is.Zero);
        }

        [Test]
        public void EmptyMapAndDefaultQueryHaveNoCandidates() {
            this.config.obstaclePositions = Array.Empty<Vector3>();
            using var empty = new ObstacleGridLookup(this.config.CreateSimulationSettings(), 1f);
            var lookup = empty.AsNative();
            Assert.That(ObstacleQueries.HasLineOfSight(Vector3.zero, Vector3.one, 1f, lookup), Is.True);
            Assert.That(ObstacleQueries.TryFindFirstHit(Vector2.zero, Vector2.one, 1f, lookup, out var t), Is.False);
            Assert.That(t, Is.EqualTo(float.MaxValue));
            var cursor = default(ObstacleQueries.BoundsQuery).GetEnumerator();
            Assert.That(cursor.MoveNext(), Is.False);
            Assert.Throws<InvalidOperationException>(() => { var unused = cursor.Current; });
        }
    }
}

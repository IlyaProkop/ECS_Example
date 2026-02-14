using System;
using System.Collections.Generic;
using Game.ECS.Components;
using Game.Spatial;
using NUnit.Framework;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.Tests {
    public sealed class SpatialSnapshotTests {
        private World world;
        [SetUp] public void SetUp() {
            this.world = World.Create("Spatial snapshot tests");
            this.world.UpdateByUnity = false;
        }
        [TearDown] public void TearDown() => this.world.Dispose();

        [TestCase(0f)]
        [TestCase(1.25f)]
        [TestCase(100f)]
        public void CircleMatchesBruteForceIncludingNegativeCellsAndBoundary(float radius) {
            var index = new EnemySpatialIndex(256);
            var center = new Vector3(-2f, 0f, -2f);
            var expected = new List<int>();
            var random = new System.Random(991);
            for (var i = 0; i < 256; i++) {
                var point = i == 0 ? center + Vector3.right * radius :
                    new Vector3((float)random.NextDouble() * 20f - 10f, 0f, (float)random.NextDouble() * 20f - 10f);
                index.Add(this.world.CreateEntity(), i, point, 0.5f);
                if ((point - center).sqrMagnitude <= radius * radius) expected.Add(i);
            }
            var actual = Collect(index.QueryCircle(center, radius));
            Assert.That(actual, Is.EquivalentTo(expected));
            Assert.That(actual, Does.Contain(0), "Circle boundary is inclusive.");
        }

        [Test]
        public void GridOrderAndLinearFallbackKeepTheirDeterministicTieRules() {
            var index = new EnemySpatialIndex(4);
            for (var i = 0; i < 4; i++) index.Add(this.world.CreateEntity(), i, Vector3.one, 0.5f);
            Assert.That(Collect(index.QueryCircle(Vector3.one, 0.1f)), Is.EqualTo(new[] { 3, 2, 1, 0 }));
            Assert.That(Collect(index.QueryCircle(Vector3.one, 100f)), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(index.TryFindNearest(Vector3.zero, out var nearest), Is.True);
            Assert.That(nearest.ActorId, Is.Zero);
        }

        [Test]
        public void IndependentCursorsDoNotConsumeEachOthersResults() {
            var index = new EnemySpatialIndex(2);
            index.Add(this.world.CreateEntity(), 10, Vector3.zero, 0.5f);
            index.Add(this.world.CreateEntity(), 20, Vector3.right, 0.5f);
            var query = index.QueryCircle(Vector3.zero, 100f);
            var first = query.GetEnumerator();
            var second = query.GetEnumerator();
            Assert.That(first.MoveNext(), Is.True);
            Assert.That(second.MoveNext(), Is.True);
            Assert.That(first.Current.ActorId, Is.EqualTo(second.Current.ActorId));
            Assert.That(first.MoveNext(), Is.True);
            Assert.That(second.Current.ActorId, Is.EqualTo(10));
            Assert.That(first.MoveNext(), Is.False);
            Assert.Throws<InvalidOperationException>(() => { var unused = first.Current; });
            Assert.That(Collect(query), Is.EqualTo(new[] { 10, 20 }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MutationInvalidatesBothCreatedAndDeferredCursors(bool rebuild) {
            var index = new EnemySpatialIndex(2);
            index.Add(this.world.CreateEntity(), 1, Vector3.zero, 0.5f);
            var query = index.QueryCircle(Vector3.zero, 1f);
            var cursor = query.GetEnumerator();
            Assert.That(cursor.MoveNext(), Is.True);
            if (rebuild) index.BeginFrame();
            else index.Add(this.world.CreateEntity(), 2, Vector3.zero, 0.5f);
            Assert.Throws<InvalidOperationException>(() => cursor.MoveNext());
            Assert.Throws<InvalidOperationException>(() => { var unused = cursor.Current; });
            Assert.Throws<InvalidOperationException>(() => query.GetEnumerator().MoveNext());
        }

        [Test]
        public void SnapshotGeometryDoesNotFollowLiveComponentWrites() {
            var enemy = this.world.CreateEntity();
            this.world.GetStash<PositionComponent>().Add(enemy).value = Vector3.right;
            var index = new EnemySpatialIndex(1);
            index.Add(enemy, 41, Vector3.right, 0.75f);
            this.world.GetStash<PositionComponent>().Get(enemy).value = Vector3.left * 10f;
            Assert.That(index.TryFindNearest(Vector3.zero, out var sample), Is.True);
            Assert.That(sample.Position, Is.EqualTo(Vector2.right));
            Assert.That(sample.Radius, Is.EqualTo(0.75f));
            Assert.That(sample.ActorId, Is.EqualTo(41));
            Assert.That(Collect(index.QueryCircle(Vector3.right, 0f)), Is.EqualTo(new[] { 41 }));
        }

        [Test]
        public void RemovedEntityCannotBeUsedThroughAnOldSample() {
            var enemy = this.world.CreateEntity();
            var index = new EnemySpatialIndex(1);
            index.Add(enemy, 1, Vector3.zero, 0.5f);
            Assert.That(index.TryFindNearest(Vector3.zero, out var sample), Is.True);
            this.world.RemoveEntity(enemy);
            this.world.Commit();
            this.world.CreateEntity(); // Morpeh may recycle the entity object; generation must still match.
            Assert.That(sample.IsAlive, Is.False);
            Assert.That(index.TryFindNearest(Vector3.zero, out _), Is.False);
            index.BeginFrame();
            Assert.That(Collect(index.QueryCircle(Vector3.zero, 2f)), Is.Empty);
        }

        [Test]
        public void OverflowLeavesThePublishedDataAndCursorIntact() {
            var index = new EnemySpatialIndex(1);
            index.Add(this.world.CreateEntity(), 1, Vector3.zero, 0.5f);
            var query = index.QueryCircle(Vector3.zero, 2f);
            Assert.Throws<InvalidOperationException>(() => index.Add(this.world.CreateEntity(), 2, Vector3.zero, 0.5f));
            Assert.That(Collect(query), Is.EqualTo(new[] { 1 }));
        }

        private static List<int> Collect(EnemySpatialIndex.CircleQuery query) {
            var result = new List<int>();
            foreach (var sample in query) result.Add(sample.ActorId);
            return result;
        }

        [Test]
        public void GeometryBorrowRemovesStaleHandlesWithoutChangingNeighborOrder() {
            var index = new EnemySpatialIndex(4);
            var removed = this.world.CreateEntity();
            index.Add(this.world.CreateEntity(), 1, Vector3.one, 0.5f);
            index.Add(removed, 2, Vector3.one, 1f);
            index.Add(this.world.CreateEntity(), 3, Vector3.one, 0.75f);
            index.Add(this.world.CreateEntity(), 4, Vector3.one, 0.5f);
            var oldView = index.BorrowLiveGeometry();
            this.world.RemoveEntity(removed);
            this.world.Commit();
            this.world.CreateEntity();
            var live = index.BorrowLiveGeometry();
            Assert.That(Collect(live.QueryCircle(Vector3.one, 0.1f)), Is.EqualTo(new[] { 4, 3, 1 }));
            Assert.That(Collect(live.QueryCircle(Vector3.one, 100f)), Is.EqualTo(new[] { 1, 3, 4 }));
            Assert.Throws<InvalidOperationException>(() => oldView.QueryCircle(Vector3.one, 1f));
        }

        [Test]
        public void ValueViewsAndCursorsAreInvalidatedByRebuildAndSlotReuse() {
            var index = new EnemySpatialIndex(1);
            index.Add(this.world.CreateEntity(), 1, Vector3.one, 0.5f);
            var view = index.BorrowLiveGeometry();
            var cursor = view.QueryCircle(Vector3.one, 1f).GetEnumerator();
            Assert.That(cursor.MoveNext(), Is.True);
            index.BeginFrame();
            index.Add(this.world.CreateEntity(), 2, Vector3.zero, 1f);
            Assert.Throws<InvalidOperationException>(() => view.QueryCircle(Vector3.one, 1f));
            Assert.Throws<InvalidOperationException>(() => cursor.MoveNext());
            Assert.Throws<InvalidOperationException>(() => { var unused = cursor.Current; });
            Assert.That(Collect(index.BorrowLiveGeometry().QueryCircle(Vector3.zero, 1f)), Is.EqualTo(new[] { 2 }));
        }

        private static List<int> Collect(EnemyGeometryIndex.CircleQuery query) {
            var result = new List<int>();
            foreach (var sample in query) result.Add(sample.ActorId);
            return result;
        }
    }
}

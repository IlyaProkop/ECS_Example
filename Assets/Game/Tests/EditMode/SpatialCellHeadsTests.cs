using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Spatial;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests {
    public sealed class SpatialCellHeadsTests {
        [Test]
        public void BoundedTableMatchesDictionaryAcrossReplacementAndRepeatedRebuilds() {
            const int capacity = 129;
            var table = new SpatialCellHeads(capacity);
            var expected = new Dictionary<Vector2Int, int>();
            var previous = new List<Vector2Int>();
            var random = new System.Random(8371);
            for (var frame = 0; frame < 40; frame++) {
                table.Clear();
                foreach (var cell in previous) Assert.That(table.Find(cell), Is.EqualTo(-1), "Previous frame must be invisible.");
                expected.Clear();
                previous.Clear();
                for (var i = 0; i < capacity; i++) {
                    var cell = new Vector2Int(random.Next(-100000, 100000), random.Next(-100000, 100000));
                    var old = expected.TryGetValue(cell, out var value) ? value : -1;
                    Assert.That(table.Replace(cell, i), Is.EqualTo(old));
                    expected[cell] = i;
                    previous.Add(cell);
                }
                // Replace existing heads at full capacity; probing collisions cannot consume capacity.
                for (var i = 0; i < previous.Count; i++) {
                    var cell = previous[i];
                    Assert.That(table.Replace(cell, i + capacity), Is.EqualTo(expected[cell]));
                    expected[cell] = i + capacity;
                }
                foreach (var pair in expected) Assert.That(table.Find(pair.Key), Is.EqualTo(pair.Value));
                Assert.That(table.Find(new Vector2Int(int.MinValue, int.MaxValue)), Is.EqualTo(-1));
            }
        }

        [Test]
        public void OverflowIsAtomicAndGenerationWrapDoesNotResurrectCells() {
            var table = new SpatialCellHeads(1);
            table.Replace(Vector2Int.zero, 12);
            Assert.Throws<InvalidOperationException>(() => table.Replace(Vector2Int.one, 99));
            Assert.That(table.Find(Vector2Int.zero), Is.EqualTo(12));
            Assert.That(table.Find(Vector2Int.one), Is.EqualTo(-1));
            // Reproduce the wrap without running billions of frames. The old stamp is 1.
            typeof(SpatialCellHeads).GetField("generation", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(table, uint.MaxValue);
            table.Clear();
            Assert.That(table.Find(Vector2Int.zero), Is.EqualTo(-1));
            Assert.That(table.Replace(Vector2Int.one, 7), Is.EqualTo(-1));
            Assert.That(table.Find(Vector2Int.one), Is.EqualTo(7));
        }

        [Test]
        public void ZeroCapacityStillSupportsEmptyLookupsAndClearing() {
            var table = new SpatialCellHeads(0);
            Assert.That(table.Find(Vector2Int.zero), Is.EqualTo(-1));
            Assert.Throws<InvalidOperationException>(() => table.Replace(Vector2Int.zero, 0));
            table.Clear();
            Assert.That(table.Find(Vector2Int.zero), Is.EqualTo(-1));
        }
    }
}

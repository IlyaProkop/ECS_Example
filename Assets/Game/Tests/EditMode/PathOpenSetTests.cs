using System;
using System.Collections.Generic;
using Game.ECS.Navigation;
using NUnit.Framework;

namespace Game.Tests {
    public sealed class PathOpenSetTests {
        [Test]
        public void LoweredCostKeepsOriginalTieOrderAndDoesNotDuplicateTheNode() {
            var queue = new PathOpenSet(4);
            queue.AddOrDecrease(2, 9f);
            queue.AddOrDecrease(0, 5f);
            queue.AddOrDecrease(1, 5f);
            queue.AddOrDecrease(2, 5f);
            queue.AddOrDecrease(1, 8f);
            Assert.That(queue.Count, Is.EqualTo(3));
            Assert.That(queue.Pop(), Is.EqualTo(2));
            Assert.That(queue.Pop(), Is.EqualTo(0));
            Assert.That(queue.Pop(), Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => queue.Pop());
        }

        [Test]
        public void ClearAfterAnEarlyExitAllowsReuseOfEveryNode() {
            var queue = new PathOpenSet(32);
            for (var i = 0; i < 32; i++) queue.AddOrDecrease(i, 32 - i);
            queue.Pop();
            queue.Clear();
            for (var i = 0; i < 32; i++) queue.AddOrDecrease(i, 100 + i);
            for (var i = 0; i < 32; i++) Assert.That(queue.Pop(), Is.EqualTo(i));
        }

        [Test]
        public void MixedOperationsMatchAStableSortedReference() {
            var queue = new PathOpenSet(128);
            var reference = new List<int>();
            var costs = new float[128];
            var random = new Random(1937);
            for (var operation = 0; operation < 10000; operation++) {
                if (operation % 431 == 0) { queue.Clear(); reference.Clear(); }
                if (reference.Count > 0 && random.Next(3) == 0) {
                    var best = 0;
                    for (var i = 1; i < reference.Count; i++)
                        if (costs[reference[i]] < costs[reference[best]]) best = i;
                    Assert.That(queue.Pop(), Is.EqualTo(reference[best]), "Operation " + operation);
                    reference.RemoveAt(best);
                } else {
                    var node = random.Next(128);
                    var cost = random.Next(20); // Many ties and repeated decrease-key operations.
                    queue.AddOrDecrease(node, cost);
                    if (!reference.Contains(node)) { reference.Add(node); costs[node] = cost; }
                    else costs[node] = Math.Min(costs[node], cost);
                }
                Assert.That(queue.Count, Is.EqualTo(reference.Count));
            }
        }
    }
}

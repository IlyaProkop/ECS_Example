using NUnit.Framework;
using Scellecs.Morpeh;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

namespace Game.Tests {
    public sealed class MorpehMetricsTests {
        [Test]
        public void EmptyWorldMetricsDoNotAllocateAnEnumerator() {
            using var world = World.Create("Metrics allocation regression");
            world.UpdateByUnity = false;
            world.CleanupUpdate(0f);
            AssertCleanupDoesNotAllocate(world);
            Assert.That(world.metrics.systems, Is.Zero);
        }

        [Test]
        public void MetricsStillCountEverySystemGroupOnEachCleanup() {
            using var world = World.Create("Metrics group regression");
            world.UpdateByUnity = false;
            var first = world.CreateSystemsGroup();
            first.AddSystem(new NoOp()); first.AddSystem(new NoOp());
            var second = world.CreateSystemsGroup();
            second.AddSystem(new NoOp());
            world.AddSystemsGroup(-10, first); world.AddSystemsGroup(30, second);
            world.Update(0f);
            world.CleanupUpdate(0f);
            Assert.That(world.metrics.systems, Is.EqualTo(3));
            world.CleanupUpdate(0f);
            Assert.That(world.metrics.systems, Is.EqualTo(3), "Metrics must reset instead of accumulating each frame.");
            AssertCleanupDoesNotAllocate(world);
        }
        private static void AssertCleanupDoesNotAllocate(World world) {
            TestDelegate cleanup = () => world.CleanupUpdate(0f);
            cleanup(); // Include the delegate's inlined body in JIT warmup.
            Assert.That(cleanup, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory());
        }
        private sealed class NoOp : ISystem {
            public World World { get; set; }
            public void OnAwake() { }
            public void OnUpdate(float deltaTime) { }
            public void Dispose() { }
        }
    }
}

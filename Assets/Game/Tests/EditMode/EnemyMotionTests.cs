using System;
using Game.Config;
using Game.Domain;
using Game.ECS.Movement;
using Game.Spatial;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class EnemyMotionTests {
        private GameConfig authoring;
        private SimulationSettings config;
        private ObstacleGridLookup obstacles;

        [SetUp]
        public void SetUp() {
            this.authoring = ScriptableObject.CreateInstance<GameConfig>();
            this.authoring.obstaclePositions = Array.Empty<Vector3>();
            this.config = this.authoring.CreateSimulationSettings();
            this.obstacles = new ObstacleGridLookup(this.config, 1f);
        }
        [TearDown] public void TearDown() { this.obstacles.Dispose(); Object.DestroyImmediate(this.authoring); }

        [Test]
        public void ReorderingAndPartitioningInputDoesNotChangeActorResults() {
            var geometry = new EnemyGeometryIndex(64);
            var inputs = new EnemyMotionInput[64];
            var player = new Vector3(-6f, 0f, -6f);
            for (var i = 0; i < inputs.Length; i++) {
                var position = new Vector3(i % 8 * 0.45f, 0f, i / 8 * 0.55f);
                var radius = i % 2 == 0 ? 0.5f : 1f;
                inputs[i] = Pursuit(i + 1, position, Vector3.right * 0.4f, player, radius, 2.8f, 1.5f);
                geometry.Add(new EnemyGeometrySample(i + 1, position, radius));
            }
            var view = geometry.Borrow();
            var frame = new EnemyMotionFrame(player, 0.5f, 1f, 1f / 60f);
            var expected = new EnemyMotionResult[inputs.Length];
            EnemyMotionSolver.Solve(inputs, expected, frame, view, this.config.arenaHalfSize, this.obstacles.AsNative());
            Array.Reverse(inputs);
            var actual = new EnemyMotionResult[inputs.Length];
            for (var offset = 0; offset < inputs.Length; offset += 7) {
                var count = Math.Min(7, inputs.Length - offset);
                EnemyMotionSolver.Solve(inputs.AsSpan(offset, count), actual.AsSpan(offset, count), frame, view, this.config.arenaHalfSize, this.obstacles.AsNative());
            }
            for (var i = 0; i < inputs.Length; i++) {
                var reference = expected[inputs[i].ActorId - 1];
                Assert.That(actual[i].Position, Is.EqualTo(reference.Position));
                Assert.That(actual[i].Velocity, Is.EqualTo(reference.Velocity));
            }
        }

        [Test]
        public void CoincidentActorsSeparateInOppositeDirectionsUsingStableIds() {
            var position = new Vector3(4f, 0f, 4f);
            var inputs = new[] {
                Pursuit(1, position, Vector3.zero, Vector3.zero, 0.5f, 0f, 1.5f),
                Pursuit(2, position, Vector3.zero, Vector3.zero, 0.5f, 0f, 1.5f)
            };
            var geometry = new EnemyGeometryIndex(2);
            foreach (var input in inputs) geometry.Add(new EnemyGeometrySample(input.ActorId, input.Position, input.Radius));
            var results = new EnemyMotionResult[2];
            EnemyMotionSolver.Solve(inputs, results, new EnemyMotionFrame(Vector3.zero, 0.5f, 0.5f, 1f / 60f), geometry.Borrow(), this.config.arenaHalfSize, this.obstacles.AsNative());
            Assert.That((Vector3)results[0].Position, Is.EqualTo(position + Vector3.left * 0.5f));
            Assert.That((Vector3)results[1].Position, Is.EqualTo(position + Vector3.right * 0.5f));
        }

        [TestCase(0.5f)]
        [TestCase(1f)]
        public void LargeDisplacementCannotTunnelThroughObstacleForEitherRadius(float radius) {
            this.authoring.obstaclePositions = new[] { Vector3.zero };
            this.authoring.obstacleScale = Vector3.one * 2f;
            var settings = this.authoring.CreateSimulationSettings();
            using var map = new ObstacleGridLookup(settings, radius);
            var start = Vector3.left * 4f;
            var player = Vector3.right * 6f;
            var inputs = new[] { Pursuit(1, start, Vector3.zero, player, radius, 30f, 1.5f) };
            var geometry = new EnemyGeometryIndex(1);
            geometry.Add(new EnemyGeometrySample(1, start, radius));
            var results = new EnemyMotionResult[1];
            EnemyMotionSolver.Solve(inputs, results, new EnemyMotionFrame(player, 0.5f, radius, 0.4f), geometry.Borrow(), settings.arenaHalfSize, map.AsNative());
            Assert.That(results[0].Position.x, Is.GreaterThan(start.x).And.LessThanOrEqualTo(-1f - radius));
            Assert.That(new ActorMotionQuery(settings.arenaHalfSize, radius, true, map.AsNative()).IsBlocked(results[0].Position), Is.False);
        }

        [Test]
        public void ShortOutputFailsBeforeWritingAnyResults() {
            var inputs = new EnemyMotionInput[2];
            var results = new[] { new EnemyMotionResult(Vector3.one, Vector3.right) };
            Assert.Throws<ArgumentException>(() => EnemyMotionSolver.Solve(inputs, results, default, default, this.config.arenaHalfSize, this.obstacles.AsNative()));
            Assert.That((Vector3)results[0].Position, Is.EqualTo(Vector3.one));
            Assert.That((Vector3)results[0].Velocity, Is.EqualTo(Vector3.right));
        }

        [TestCase(1)]
        [TestCase(64)]
        public void ParallelKernelMatchesManagedMotionWithCrowdingAndObstacles(int batchSize) {
            this.authoring.obstaclePositions = new[] { Vector3.zero, Vector3.right * 5f };
            var settings = this.authoring.CreateSimulationSettings();
            using var map = new ObstacleGridLookup(settings, 1f);
            var inputs = new EnemyMotionInput[512];
            var geometry = new EnemyGeometryIndex(inputs.Length);
            var random = new System.Random(145);
            var player = new Vector3(-6f, 0f, -6f);
            for (var i = 0; i < inputs.Length; i++) {
                var position = new Vector3((float)random.NextDouble() * 24f - 12f, 0f, (float)random.NextDouble() * 16f - 8f);
                if (i < 2) position = new Vector3(-4f, 0f, -4f);
                var radius = i % 2 == 0 ? 0.5f : 1f;
                inputs[i] = Pursuit(i + 1, position, Vector3.right * 0.4f, player, radius, 2.8f, 1.5f);
                geometry.Add(new EnemyGeometrySample(i + 1, position, radius));
            }
            geometry.Disable(15);
            using var native = new NativeEnemyGeometrySnapshot(inputs.Length);
            geometry.Borrow().CopyTo(native);
            using var inputBuffer = new NativeArray<EnemyMotionInput>(inputs, Allocator.TempJob);
            using var outputBuffer = new NativeArray<EnemyMotionResult>(inputs.Length, Allocator.TempJob);
            var frame = new EnemyMotionFrame(player, 0.5f, 1f, 1f / 60f);
            var expected = new EnemyMotionResult[inputs.Length];
            EnemyMotionSolver.Solve(inputs, expected, frame, geometry.Borrow(), settings.arenaHalfSize, map.AsNative());
            new EnemyMotionJob { Inputs = inputBuffer, Results = outputBuffer, Neighbors = native.Borrow(),
                Frame = frame, ArenaHalfSize = settings.arenaHalfSize, Obstacles = map.AsNative()
            }.Schedule(inputs.Length, batchSize).Complete();
            for (var i = 0; i < inputs.Length; i++) {
                Assert.That(Vector3.Distance(outputBuffer[i].Position, expected[i].Position), Is.Zero, $"Position {i}");
                Assert.That(Vector3.Distance(outputBuffer[i].Velocity, expected[i].Velocity), Is.Zero, $"Velocity {i}");
                var desired = (Vector3)inputs[i].DesiredVelocity;
                var legacy = Vector3.MoveTowards(inputs[i].Velocity, desired, inputs[i].Speed * 18f * frame.DeltaTime);
                Assert.That(Vector3.Distance(expected[i].Velocity, legacy), Is.Zero,
                    $"Legacy velocity {i}: new {((Vector3)expected[i].Velocity).ToString("R")} old {legacy.ToString("R")} desired {desired.ToString("R")}");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SymmetricCrowdDoesNotDriftAwayFromStationaryPlayer(bool parallel) {
            const int count = 64;
            var inputs = new EnemyMotionInput[count];
            var results = new EnemyMotionResult[count];
            var geometry = new EnemyGeometryIndex(count);
            using var native = new NativeEnemyGeometrySnapshot(count);
            using var inputBuffer = new NativeArray<EnemyMotionInput>(count, Allocator.TempJob);
            using var outputBuffer = new NativeArray<EnemyMotionResult>(count, Allocator.TempJob);
            var frame = new EnemyMotionFrame(Vector3.zero, 0.5f, 0.5f, 1f / 60f);
            for (var i = 0; i < count; i++) {
                var point = new Vector3((i % 8 - 3.5f) * 1.1f, 0f, (i / 8 - 3.5f) * 1.1f);
                inputs[i] = Pursuit(i + 1, point, Vector3.zero, Vector3.zero, 0.5f, 2.8f, 1.5f);
            }
            for (var tick = 0; tick < 600; tick++) {
                geometry.BeginFrame();
                foreach (var input in inputs) geometry.Add(new EnemyGeometrySample(input.ActorId, input.Position, input.Radius));
                if (parallel) {
                    geometry.Borrow().CopyTo(native);
                    NativeArray<EnemyMotionInput>.Copy(inputs, inputBuffer);
                    new EnemyMotionJob { Inputs = inputBuffer, Results = outputBuffer, Neighbors = native.Borrow(),
                        Frame = frame, ArenaHalfSize = this.config.arenaHalfSize, Obstacles = this.obstacles.AsNative()
                    }.Schedule(count, 8).Complete();
                    NativeArray<EnemyMotionResult>.Copy(outputBuffer, results);
                } else EnemyMotionSolver.Solve(inputs, results, frame, geometry.Borrow(), this.config.arenaHalfSize, this.obstacles.AsNative());
                for (var i = 0; i < count; i++)
                    inputs[i] = Pursuit(i + 1, results[i].Position, results[i].Velocity, Vector3.zero, 0.5f, 2.8f, 1.5f);
            }
            var center = Vector3.zero;
            foreach (var result in results) center += (Vector3)result.Position / count;
            Assert.That(center.magnitude, Is.LessThan(0.1f), "A symmetric crowd must not acquire directional drift from neighbor traversal.");
            var meanNearest = 0f;
            for (var i = 0; i < count; i++) {
                var nearest = float.MaxValue;
                for (var j = 0; j < count; j++) if (i != j)
                    nearest = Mathf.Min(nearest, Vector3.Distance(results[i].Position, results[j].Position));
                Assert.That(nearest, Is.GreaterThan(0.999f), "Actors must retain their diameter during sustained approach.");
                meanNearest += nearest / count;
            }
            Assert.That(meanNearest, Is.GreaterThan(0.999f), "Soft contact response must not collapse one-meter actors into a dense point cloud.");
            TestContext.WriteLine($"Centroid offset: {center.magnitude}; mean nearest distance: {meanNearest}");
        }

        [Test]
        public void ReversingNeighborTraversalPreservesContactCorrection() {
            var contacts = new[] {
                new EnemyGeometrySample(2, new Vector3(0.3f, 0f, 0.2f), 0.5f),
                new EnemyGeometrySample(3, new Vector3(-0.4f, 0f, -0.1f), 0.5f),
                new EnemyGeometrySample(4, new Vector3(0.1f, 0f, -0.6f), 0.5f)
            };
            var input = Pursuit(1, Vector3.zero, Vector3.zero, Vector3.zero, 0.5f, 0f, 0f);
            var frame = new EnemyMotionFrame(Vector3.one * 8f, 0.5f, 0.5f, 1f / 60f);
            var forward = EnemyMotionSolver.SolveOne(input, frame, new ContactEnumerator(contacts), this.config.arenaHalfSize, this.obstacles.AsNative());
            Array.Reverse(contacts);
            var reverse = EnemyMotionSolver.SolveOne(input, frame, new ContactEnumerator(contacts), this.config.arenaHalfSize, this.obstacles.AsNative());
            Assert.That(Vector3.Distance(forward.Position, reverse.Position), Is.LessThan(0.000001f));
        }

        [TestCase(2.8f)]
        [TestCase(30f)]
        public void ApproachingActorsShareClearanceWithoutCrossingOrCompressing(float speed) {
            var inputs = new[] {
                Pursuit(1, Vector3.left * 2f, Vector3.zero, Vector3.right * 5f, 0.5f, speed, 0f),
                Pursuit(2, Vector3.right * 2f, Vector3.zero, Vector3.left * 5f, 0.5f, speed, 0f)
            };
            var results = new EnemyMotionResult[2];
            var geometry = new EnemyGeometryIndex(2);
            for (var tick = 0; tick < 120; tick++) {
                geometry.BeginFrame();
                foreach (var input in inputs) geometry.Add(new EnemyGeometrySample(input.ActorId, input.Position, input.Radius));
                EnemyMotionSolver.Solve(inputs, results, new EnemyMotionFrame(Vector3.forward * 8f, 0.5f, 0.5f, 0.1f),
                    geometry.Borrow(), this.config.arenaHalfSize, this.obstacles.AsNative());
                Assert.That(results[0].Position.x, Is.LessThanOrEqualTo(-0.5f + 0.00001f));
                Assert.That(results[1].Position.x, Is.GreaterThanOrEqualTo(0.5f - 0.00001f));
                for (var j = 0; j < 2; j++) inputs[j] = Pursuit(j + 1, results[j].Position, results[j].Velocity,
                    j == 0 ? Vector3.right * 5f : Vector3.left * 5f, 0.5f, speed, 0f);
            }
        }

        [Test]
        public void SpeedReductionDoesNotHideNeighborsFromResidualVelocity() {
            var inputs = new[] {
                Pursuit(1, Vector3.left * 1.5f, Vector3.right * 20f, Vector3.zero, 0.5f, 0f, 0f),
                Pursuit(2, Vector3.right * 1.5f, Vector3.left * 20f, Vector3.zero, 0.5f, 0f, 0f)
            };
            var geometry = new EnemyGeometryIndex(2);
            foreach (var input in inputs) geometry.Add(new EnemyGeometrySample(input.ActorId, input.Position, input.Radius));
            var results = new EnemyMotionResult[2];
            EnemyMotionSolver.Solve(inputs, results, new EnemyMotionFrame(Vector3.forward * 8f, 0.5f, 0.5f, 0.1f),
                geometry.Borrow(), this.config.arenaHalfSize, this.obstacles.AsNative());
            Assert.That(results[0].Position.x, Is.LessThanOrEqualTo(-0.5f + 0.00001f));
            Assert.That(results[1].Position.x, Is.GreaterThanOrEqualTo(0.5f - 0.00001f));
        }

        [Test]
        public void DesiredVelocityCanMoveAwayFromPlayerInsideFormerStoppingDistance() {
            var start = Vector3.right * 1.1f;
            var input = new EnemyMotionInput(1, start, Vector3.zero, Vector3.right * 2.8f, 0.5f, 2.8f);
            var geometry = new EnemyGeometryIndex(1);
            var results = new EnemyMotionResult[1];
            EnemyMotionSolver.Solve(new[] { input }, results, new EnemyMotionFrame(Vector3.zero, 0.5f, 0.5f, 1f / 60f),
                geometry.Borrow(), this.config.arenaHalfSize, this.obstacles.AsNative());
            Assert.That(results[0].Position.x, Is.GreaterThan(start.x));
        }

        [Test]
        public void RemovingFrontNeighbourAllowsWaitingActorToAdvance() {
            var geometry = new EnemyGeometryIndex(2);
            var frame = new EnemyMotionFrame(Vector3.forward * 8f, 0.5f, 0.5f, 1f / 60f);
            var input = new EnemyMotionInput(1, Vector3.zero, Vector3.right * 2f, Vector3.right * 2f, 0.5f, 2f);
            var results = new EnemyMotionResult[1];
            geometry.Add(new EnemyGeometrySample(2, Vector3.right, 0.5f));
            EnemyMotionSolver.Solve(new[] { input }, results, frame, geometry.Borrow(), this.config.arenaHalfSize, this.obstacles.AsNative());
            Assert.That(results[0].Position.x, Is.EqualTo(0f));
            geometry.BeginFrame();
            EnemyMotionSolver.Solve(new[] { input }, results, frame, geometry.Borrow(), this.config.arenaHalfSize, this.obstacles.AsNative());
            Assert.That(results[0].Position.x, Is.GreaterThan(0.03f));
        }

        private static EnemyMotionInput Pursuit(int id, Unity.Mathematics.float3 position,
            Unity.Mathematics.float3 velocity, Unity.Mathematics.float3 target, float radius, float speed, float stop) =>
            new EnemyMotionInput(id, position, velocity, PathSteering.DesiredVelocity(position, target, target, speed, stop), radius, speed);

        private struct ContactEnumerator : IEnemyGeometryEnumerator {
            private readonly EnemyGeometrySample[] samples;
            private int index;
            public ContactEnumerator(EnemyGeometrySample[] samples) { this.samples = samples; this.index = -1; }
            public EnemyGeometrySample Current => this.samples[this.index];
            public bool MoveNext() => ++this.index < this.samples.Length;
        }

        [TestCase(0.5f)]
        [TestCase(100f)]
        public void NativeGeometryPreservesQueryOrderAndDisabledSlotsAfterRebuild(float radius) {
            var geometry = new EnemyGeometryIndex(128);
            using var native = new NativeEnemyGeometrySnapshot(128);
            for (var rebuild = 0; rebuild < 3; rebuild++) {
                geometry.BeginFrame();
                for (var i = 0; i < 128 - rebuild * 30; i++)
                    geometry.Add(new EnemyGeometrySample(i + 1, new Vector3(i % 8 - 4f, 0f, i / 8 - 8f), 0.5f));
                geometry.Disable(2);
                geometry.Borrow().CopyTo(native);
                for (var x = -5; x < 5; x++) {
                    var center = new Vector3(x, 0f, -3f);
                    var reference = geometry.Borrow().QueryCircle(center, radius).GetEnumerator();
                    var actual = native.Borrow().Query(center, radius);
                    while (reference.MoveNext()) {
                        Assert.That(actual.MoveNext(), Is.True);
                        Assert.That(actual.Current.ActorId, Is.EqualTo(reference.Current.ActorId));
                    }
                    Assert.That(actual.MoveNext(), Is.False);
                }
            }
        }
    }
}

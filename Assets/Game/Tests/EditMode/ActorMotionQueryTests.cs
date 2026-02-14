using System;
using Game.Config;
using Game.Domain;
using Game.Spatial;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class ActorMotionQueryTests {
        private GameConfig authoring;
        [SetUp] public void SetUp() => this.authoring = ScriptableObject.CreateInstance<GameConfig>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(this.authoring);

        [TestCase(0, true)]
        [TestCase(4, true)]
        [TestCase(5, true)]
        [TestCase(225, true)]
        [TestCase(0, false)]
        [TestCase(4, false)]
        [TestCase(5, false)]
        [TestCase(225, false)]
        public void IndexedMovementMatchesFullScanAcrossRadiiGateClampsAndLargeSteps(int obstacleCount, bool closed) {
            this.authoring.arenaHalfSize = new Vector2(35f, 35f);
            this.authoring.obstacleScale = new Vector3(1.3f, 2f, 1.7f);
            this.authoring.obstacleLookupCellSize = 2f;
            this.authoring.obstaclePositions = new Vector3[obstacleCount];
            for (var i = 0; i < obstacleCount; i++)
                this.authoring.obstaclePositions[i] = new Vector3(i % 15 * 4f - 28f, 0f, i / 15 * 4f - 28f);
            var config = this.authoring.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(config, 1f);
            var radii = new[] { 0.15f, 0.5f, 1f };
            var queries = new ActorMotionQuery[radii.Length];
            for (var i = 0; i < radii.Length; i++)
                queries[i] = new ActorMotionQuery(config.arenaHalfSize, radii[i], closed, obstacles.AsNative());
            var random = new System.Random(24839);
            for (var i = 0; i < 160; i++) {
                // Include blocked/outside starts, zero motion, sub-cell moves and large displacements.
                var start = new Vector3(RandomCoordinate(random, 42f), i % 5 == 0 ? 3f : 0f, RandomCoordinate(random, 42f));
                var size = i % 11 == 0 ? 40f : i % 3 == 0 ? 0f : 3f;
                var displacement = new Vector3(RandomCoordinate(random, size), 0f, RandomCoordinate(random, size));
                var slot = i % radii.Length;
                var query = queries[slot];
                Assert.That(query.IsBlocked(start), Is.EqualTo(FullScanBlocked(config, start, radii[slot])), "Point " + i);
                var expected = FullScanMovement(config, start, displacement, radii[slot], closed);
                var actual = query.Move(start, displacement);
                Assert.That(actual, Is.EqualTo(expected), "Movement " + i + ": " + start + " -> " + displacement);
            }
        }

        [Test]
        public void MovementAllowsTangencyAndSlidesAlongAnObstacleFace() {
            this.authoring.obstaclePositions = new[] { Vector3.zero };
            this.authoring.obstacleScale = Vector3.one * 2f;
            var config = this.authoring.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(config, 0.5f);
            var query = new ActorMotionQuery(config.arenaHalfSize, 0.5f, true, obstacles.AsNative());
            Assert.That(query.IsBlocked(new Vector3(-1.5f, 0f, 0f)), Is.False);
            Assert.That(query.IsBlocked(new Vector3(-1.4999f, 0f, 0f)), Is.True);
            var position = query.Move(new Vector3(-1.5f, 0f, 0f), new Vector3(1f, 0f, 1f));
            Assert.That(position.x, Is.EqualTo(-1.5f));
            Assert.That(position.z, Is.EqualTo(1f).Within(0.00001f));
            Assert.That(query.IsBlocked(position), Is.False);
        }

        [TestCase(1)]
        [TestCase(5)]
        public void GateProjectionChecksAnObstacleAwayFromTheRequestedSegment(int obstacleCount) {
            this.authoring.arenaHalfSize = new Vector2(10f, 10f);
            this.authoring.obstacleScale = Vector3.one * 0.5f;
            this.authoring.obstaclePositions = new Vector3[obstacleCount];
            this.authoring.obstaclePositions[0] = new Vector3(1f, 0f, -12f);
            for (var i = 1; i < obstacleCount; i++) this.authoring.obstaclePositions[i] = new Vector3(i, 0f, 5f);
            var config = this.authoring.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(config, 0.5f);
            var query = new ActorMotionQuery(config.arenaHalfSize, 0.5f, false, obstacles.AsNative());
            var start = new Vector3(8f, 0f, -12f);
            Assert.That(query.IsBlocked(start), Is.False);
            Assert.That(query.Move(start, Vector3.zero), Is.EqualTo(start), "Gate projects X to 1.5, which is blocked.");
            var clearStart = new Vector3(8f, 0f, -14f);
            Assert.That(query.Move(clearStart, Vector3.zero), Is.EqualTo(new Vector3(1.5f, 0f, -14f)));
        }

        [Test]
        public void NativeCentersAndStrictEdgesMatchOriginalNonBinaryCoordinates() {
            var center = new Vector3(7.318731f, 0f, -6.117513f);
            this.authoring.obstaclePositions = new[] { center };
            this.authoring.obstacleScale = new Vector3(0.713539f, 1f, 1.192831f);
            var config = this.authoring.CreateSimulationSettings();
            using var obstacles = new ObstacleGridLookup(config, 0.731f);
            var lookup = obstacles.AsNative();
            Assert.That(lookup.obstacleBounds[0].center, Is.EqualTo(new Vector2(center.x, center.z)));
            var query = new ActorMotionQuery(config.arenaHalfSize, 0.731f, true, lookup);
            foreach (var sign in new[] { -1f, 1f }) {
                var edge = center.x + sign * (config.obstacleScale.x * 0.5f + 0.731f);
                var bits = BitConverter.SingleToInt32Bits(edge);
                for (var offset = -3; offset <= 3; offset++) {
                    var point = new Vector3(BitConverter.Int32BitsToSingle(bits + offset), 0f, center.z);
                    Assert.That(query.IsBlocked(point), Is.EqualTo(FullScanBlocked(config, point, 0.731f)), "Adjacent float at edge");
                }
            }
        }

        private static float RandomCoordinate(System.Random random, float magnitude) => ((float)random.NextDouble() * 2f - 1f) * magnitude;

        // Brute-force regression oracle: original stepping rules, independent of Native storage,
        // cell selection, prepared bounds and movement-candidate reuse in the production query.
        private static Vector3 FullScanMovement(SimulationSettings config, Vector3 position, Vector3 displacement, float radius, bool closed) {
            var steps = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / Mathf.Max(0.05f, radius * 0.5f)));
            var step = displacement / steps;
            for (var i = 0; i < steps; i++) {
                var next = Clamp(config, position + new Vector3(step.x, 0f, 0f), radius, closed);
                if (!FullScanBlocked(config, next, radius)) position = next;
                next = Clamp(config, position + new Vector3(0f, 0f, step.z), radius, closed);
                if (!FullScanBlocked(config, next, radius)) position = next;
            }
            return position;
        }

        private static bool FullScanBlocked(SimulationSettings config, Vector3 position, float radius) {
            foreach (var center in config.obstaclePositions) {
                if (Mathf.Abs(position.x - center.x) < Mathf.Abs(config.obstacleScale.x) * 0.5f + radius &&
                    Mathf.Abs(position.z - center.z) < Mathf.Abs(config.obstacleScale.z) * 0.5f + radius) return true;
            }
            return false;
        }

        private static Vector3 Clamp(SimulationSettings config, Vector3 position, float radius, bool closed) {
            position.x = Mathf.Clamp(position.x, -config.arenaHalfSize.x + radius, config.arenaHalfSize.x - radius);
            position.z = Mathf.Clamp(position.z, closed ? -config.arenaHalfSize.y + radius : -config.arenaHalfSize.y - 4f, config.arenaHalfSize.y - radius);
            if (!closed && position.z < -config.arenaHalfSize.y + radius)
                position.x = Mathf.Clamp(position.x, -2f + radius, 2f - radius);
            position.y = 0f;
            return position;
        }
    }
}

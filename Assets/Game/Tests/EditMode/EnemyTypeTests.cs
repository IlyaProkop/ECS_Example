using System;
using System.Collections.Generic;
using Game.Config;
using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.ECS.Spawning;
using Game.ECS.Systems;
using Game.Spatial;
using NUnit.Framework;
using Scellecs.Morpeh;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class EnemyTypeTests {
        [Test]
        public void AuthoredArenaContainsTwoDifferentEnemyTypes() {
            var config = Resources.Load<GameConfig>("GameConfig").CreateSimulationSettings();
            Assert.That(config.EnemyTypes.Count, Is.EqualTo(2));
            Assert.That(config.EnemyTypes[1].Radius, Is.GreaterThan(config.EnemyTypes[0].Radius));
            Assert.That(config.EnemyTypes[1].AttackRange, Is.GreaterThan(config.EnemyTypes[0].AttackRange));
        }

        [Test]
        public void RunningDefinitionsAreIsolatedFromAuthoringEditsAndArrayReplacement() {
            using var f = new Fixture();
            f.Authoring.enemyTypes[1].radius = 8f;
            f.Authoring.enemyTypes[0] = null;
            Assert.That(f.Context.Config.EnemyTypes[1].Radius, Is.EqualTo(1f));
            Assert.That(f.Context.Config.EnemyForSpawn(0).Id, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() => ((IList<EnemyDefinition>)f.Context.Config.EnemyTypes)[0] = null);
        }

        [Test]
        public void InvalidEnemyContentFailsBeforeCreatingAWorld() {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try {
                config.enemyTypes = new[] { new EnemyAuthoring(), new EnemyAuthoring() };
                Assert.Throws<ArgumentException>(() => config.CreateSimulationSettings(), "Duplicate IDs");
                config.enemyTypes = new[] { new EnemyAuthoring { radius = 2f } };
                Assert.Throws<ArgumentException>(() => config.CreateSimulationSettings(), "Cannot reach a non-overlapping player");
                config.enemyTypes = new[] { new EnemyAuthoring { radius = 10f, attackRange = 12f } };
                Assert.Throws<ArgumentException>(() => config.CreateSimulationSettings(), "Cannot fit the arena");
                config.enemyTypes = new[] { new EnemyAuthoring { attackRange = 1f } };
                Assert.Throws<ArgumentException>(() => config.CreateSimulationSettings(), "Stops outside attack range");
                config.enemyTypes = new[] { new EnemyAuthoring { radius = float.NaN } };
                Assert.Throws<ArgumentOutOfRangeException>(() => config.CreateSimulationSettings());
            } finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void MixedWaveUsesSharedPlacementWithoutOverlappingActorsOrObstacles() {
            using var f = new Fixture(config => {
                config.enemyCount = 6;
                config.obstaclePositions = new[] { Vector3.right * 5f };
            });
            var spawn = new EnemySpawnSystem(f.Context.Config, f.Context.EntityLookup, f.Context.SpawnRandom, f.Factory.Enemies, f.Context.ObstacleGridLookup);
            f.Initialize(spawn);
            for (var tick = 0; tick < 30; tick++) { spawn.OnUpdate(1f); f.World.Commit(); }
            spawn.Dispose();
            var actors = new List<Entity>();
            foreach (var enemy in f.World.Filter.With<EnemyTag>().Build()) actors.Add(enemy);
            Assert.That(actors.Count, Is.EqualTo(6));
            for (var i = 0; i < actors.Count; i++) {
                var enemy = actors[i];
                var definition = f.Context.Config.EnemyForSpawn(i);
                var position = f.Position(enemy);
                var radius = f.World.GetStash<RadiusComponent>().Get(enemy).value;
                Assert.That(f.World.GetStash<EnemyTypeComponent>().Get(enemy).id, Is.EqualTo(definition.Id));
                Assert.That(radius, Is.EqualTo(definition.Radius));
                Assert.That(f.World.GetStash<HealthComponent>().Get(enemy).max, Is.EqualTo(definition.MaxHealth));
                Assert.That(ArenaGeometry.IsInside(f.Context.Config, position, radius), Is.True);
                Assert.That(new ActorMotionQuery(f.Context.Config.arenaHalfSize, radius, true, f.Context.ObstacleGridLookup.AsNative()).IsBlocked(position), Is.False);
                for (var j = 0; j < i; j++) {
                    var clearance = radius + f.World.GetStash<RadiusComponent>().Get(actors[j]).value;
                    Assert.That((position - f.Position(actors[j])).sqrMagnitude, Is.GreaterThanOrEqualTo(clearance * clearance));
                }
            }
        }

        [Test]
        public void FailedPlacementDoesNotSkipThePendingEnemyType() {
            using var f = new Fixture();
            var spawn = new EnemySpawnSystem(f.Context.Config, f.Context.EntityLookup, new CenterRandom(), f.Factory.Enemies, f.Context.ObstacleGridLookup);
            f.Initialize(spawn);
            spawn.OnUpdate(1f); // Center is occupied by the player.
            Assert.That(f.World.GetStash<GameStateComponent>().Get(f.State).spawnedEnemies, Is.Zero);
            f.World.GetStash<PositionComponent>().Get(f.Player).value = Vector3.right * 8f;
            spawn.OnUpdate(1f); f.World.Commit();
            foreach (var enemy in f.World.Filter.With<EnemyTag>().Build()) {
                Assert.That(f.World.GetStash<EnemyTypeComponent>().Get(enemy).id, Is.EqualTo(1));
                f.World.GetStash<PositionComponent>().Get(enemy).value = Vector3.left * 8f;
            }
            spawn.OnUpdate(1f); f.World.Commit();
            Assert.That(f.World.GetStash<GameStateComponent>().Get(f.State).spawnedEnemies, Is.EqualTo(2));
            Assert.That(f.Context.Config.EnemyForSpawn(1).Id, Is.EqualTo(2));
            spawn.Dispose();
        }

        [Test]
        public void ProjectileHitsLargeEnemyAtItsEdgeOutsideTheSmallEnemyQueryRadius() {
            using var f = new Fixture();
            var heavy = f.Spawn(1, new Vector3(0.1f, 0f, 1.05f));
            f.RebuildIndex();
            f.Factory.Projectiles.Spawn(new ProjectileSpawn(Vector3.zero, Vector3.right, 12f, 10f, 0.1f, 2f,
                new DamageSourceComponent { owner = f.Player.ID, team = Team.Player, criticalMultiplier = 1f }));
            f.Run(new Game.ECS.Projectiles.ProjectileMotionSystem(f.Context.EntityLookup, f.Context.Projectiles), GameSession.FixedStep);
            f.Run(new ProjectileHitSystem(f.Context.EntityLookup,
                f.Context.EnemySpatialIndex, f.Context.ObstacleGridLookup, f.Context.Projectiles), GameSession.FixedStep);
            Assert.That(f.Context.DamageRequests.Count, Is.EqualTo(1));
            Assert.That(f.Context.DamageRequests.Items[0].target, Is.SameAs(heavy));
        }

        [Test]
        public void PlayerSeparationUsesTheActualEnemyRadius() {
            using var f = new Fixture();
            var heavy = f.Spawn(1, Vector3.right * 1.4f);
            f.RebuildIndex();
            f.Run(new PlayerEnemySeparationSystem(f.Context.Config, f.Context.EntityLookup, f.Context.EnemySpatialIndex, f.Context.ObstacleGridLookup));
            Assert.That(Vector3.Distance(f.Position(f.Player), f.Position(heavy)), Is.EqualTo(1.5f).Within(0.00001f));
        }

        [Test]
        public void SmallEnemySeesALargeNeighbourDuringMovement() {
            using var f = new Fixture(config => { foreach (var type in config.enemyTypes) type.moveSpeed = 0f; });
            f.World.GetStash<PositionComponent>().Get(f.Player).value = new Vector3(8f, 0f, 8f);
            var small = f.Spawn(0, Vector3.zero);
            var heavy = f.Spawn(1, Vector3.right * 1.3f);
            f.RebuildIndex();
            f.Run(new ChasePlayerSystem(f.Context.EntityLookup), GameSession.FixedStep);
            f.Run(new EnemyMovementSystem(f.Context.Config.Navigation, f.Context.Config.Arena, f.Context.Config.enemyCount, f.Context.EntityLookup, f.Context.Paths, f.Context.ObstacleGridLookup), GameSession.FixedStep);
            Assert.That(f.Position(small).x, Is.LessThan(-0.09f));
            Assert.That(f.Position(heavy).x, Is.GreaterThan(1.39f));
        }

        [Test]
        public void ContactRangeIsIndependentOfStoppingDistanceAndOtherEnemyTypes() {
            using var f = new Fixture();
            f.Spawn(0, Vector3.left * 2.55f);
            var heavy = f.Spawn(1, Vector3.right * 2.55f); // Beyond this type's stop distance (2.4).
            f.RebuildIndex();
            f.Run(new EnemyContactDamageSystem(f.Context.EntityLookup, f.Context.EnemySpatialIndex, f.Context.DamageRequests), 0.5f);
            Assert.That(f.Context.DamageRequests.Count, Is.EqualTo(1));
            Assert.That(f.Context.DamageRequests.Items[0].source.owner, Is.EqualTo(heavy.ID));
            Assert.That(f.Context.DamageRequests.Items[0].value, Is.EqualTo(4f));
        }

        [TestCase(0.8f)]
        [TestCase(100f)]
        public void DirectMotionGeometryMatchesCombatOrderAcrossBackendSwitchesAndRemoval(float queryRadius) {
            using var f = new Fixture();
            var small = f.Spawn(0, new Vector3(-2f, 0f, -2f));
            var heavy = f.Spawn(1, new Vector3(-2.4f, 0f, -2f));
            f.World.GetStash<EnemyPathComponent>().Remove(heavy); // Still a physical neighbour.
            f.Spawn(0, new Vector3(3f, 0f, 3f));
            using var geometry = new Game.ECS.Movement.EnemyMotionGeometry(f.Context.Config.enemyCount, true);
            geometry.Initialize(f.World);
            for (var phase = 0; phase < 3; phase++) {
                if (phase == 1) f.World.GetStash<HealthComponent>().Get(heavy).current = 0f;
                if (phase == 2) f.World.GetStash<DestroyTag>().Add(small);
                f.RebuildIndex();
                var center = new Vector3(-2f, 0f, -2f);
                var expected = new System.Collections.Generic.List<int>();
                foreach (var sample in f.Context.EnemySpatialIndex.QueryCircle(center, queryRadius)) expected.Add(sample.ActorId);
                foreach (var parallel in new[] { false, true, false }) {
                    Assert.That(geometry.Capture(parallel), Is.EqualTo(f.Context.EnemySpatialIndex.MaxRadius));
                    var actual = new System.Collections.Generic.List<int>();
                    if (parallel) {
                        var cursor = geometry.Native.Query(center, queryRadius);
                        while (cursor.MoveNext()) actual.Add(cursor.Current.ActorId);
                    } else {
                        foreach (var sample in geometry.Managed.QueryCircle(center, queryRadius)) actual.Add(sample.ActorId);
                    }
                    Assert.That(actual, Is.EqualTo(expected), $"Phase {phase}, parallel {parallel}");
                }
            }
        }

        [Test]
        public void DeadAndDestroyedEnemiesDoNotInflateTheNextSpatialSnapshot() {
            using var f = new Fixture();
            var small = f.Spawn(0, Vector3.left * 3f);
            var heavy = f.Spawn(1, Vector3.right * 3f);
            f.RebuildIndex();
            Assert.That(f.Context.EnemySpatialIndex.MaxRadius, Is.EqualTo(1f));
            f.World.GetStash<HealthComponent>().Get(heavy).current = 0f;
            f.RebuildIndex();
            Assert.That(f.Context.EnemySpatialIndex.MaxRadius, Is.EqualTo(0.5f));
            Assert.That(f.Context.EnemySpatialIndex.MaxAttackRange, Is.EqualTo(1.55f));
            f.World.GetStash<DestroyTag>().Add(small);
            f.RebuildIndex();
            Assert.That(f.Context.EnemySpatialIndex.MaxRadius, Is.Zero);
            Assert.That(f.Context.EnemySpatialIndex.MaxAttackRange, Is.Zero);
        }

        [Test]
        public void NavigationUsesEachRadiusForVisibilityAndRouteClearance() {
            using var f = new Fixture(config => {
                config.obstaclePositions = new[] { new Vector3(0f, 0f, 1.7f) };
                config.obstacleScale = Vector3.one * 2f;
                config.enemyPathRepathInterval = 0f;
            });
            f.World.GetStash<PositionComponent>().Get(f.Player).value = Vector3.right * 5f;
            var small = f.Spawn(0, Vector3.left * 5f);
            var heavy = f.Spawn(1, Vector3.left * 5f);
            f.Run(new ChasePlayerSystem(f.Context.EntityLookup), GameSession.FixedStep);
            f.Run(new EnemyPathPlanningSystem(f.Context.Config.Navigation, f.Context.Config.Arena, f.Context.Config.enemyCount, f.Context.EntityLookup,
                f.Context.ObstacleGridLookup, f.Context.Paths, f.Context.NavigationRandom), GameSession.FixedStep);
            Assert.That(f.World.GetStash<EnemyPathComponent>().Get(small).status, Is.EqualTo(PathStatus.Direct));
            var route = f.World.GetStash<EnemyPathComponent>().Get(heavy);
            Assert.That(route.status, Is.EqualTo(PathStatus.Route));
            Assert.That(route.waypointCount, Is.GreaterThan(0));
            for (var i = 0; i < route.waypointCount; i++)
                Assert.That(new ActorMotionQuery(f.Context.Config.arenaHalfSize, 1f, true, f.Context.ObstacleGridLookup.AsNative()).IsBlocked(f.Context.Paths.Get(route.slot, i)), Is.False);
        }

        [Test]
        public void ThirdConfiguredTypeUsesTheSameSpawnerAndComponentSchema() {
            using var f = new Fixture(config => config.enemyTypes = new[] {
                new EnemyAuthoring(), Heavy(), new EnemyAuthoring { id = 73, radius = 0.75f, maxHealth = 75f, moveSpeed = 4f, armor = 30f, attackRange = 3f }
            });
            var enemy = f.Spawn(2, Vector3.right * 4f);
            Assert.That(f.World.GetStash<EnemyTypeComponent>().Get(enemy).id, Is.EqualTo(73));
            Assert.That(f.World.GetStash<RadiusComponent>().Get(enemy).value, Is.EqualTo(0.75f));
            Assert.That(f.World.GetStash<MoveSpeedComponent>().Get(enemy).value, Is.EqualTo(4f));
            Assert.That(f.World.GetStash<DefenseComponent>().Get(enemy).armor, Is.EqualTo(30f));
        }

        private static EnemyAuthoring Heavy() => new EnemyAuthoring { id = 2, radius = 1f, maxHealth = 120f,
            moveSpeed = 1.8f, armor = 20f, stopDistance = 2.4f, attackRange = 2.6f, damagePerSecond = 8f };

        private sealed class CenterRandom : System.Random { public override double NextDouble() => 0.5; }
        [Test]
        public void FixedGoalRoutesAwayFromPlayerWithoutBeingOverwrittenByChase() {
            using var f = new Fixture(config => config.enemyPathRepathInterval = 0f);
            var enemy = f.Spawn(0, Vector3.right * 2f);
            f.World.GetStash<ChasePlayerTag>().Remove(enemy);
            f.World.GetStash<MovementGoalComponent>().Set(enemy, new MovementGoalComponent {
                position = Vector3.right * 7f, stoppingDistance = 0.1f, active = true
            });
            f.Run(new ChasePlayerSystem(f.Context.EntityLookup), GameSession.FixedStep);
            f.Run(new EnemyPathPlanningSystem(f.Context.Config.Navigation, f.Context.Config.Arena, f.Context.Config.enemyCount,
                f.Context.EntityLookup, f.Context.ObstacleGridLookup, f.Context.Paths, f.Context.NavigationRandom), GameSession.FixedStep);
            Assert.That(f.World.GetStash<EnemyPathComponent>().Get(enemy).lastTargetPosition, Is.EqualTo(Vector3.right * 7f));
            f.Run(new EnemyMovementSystem(f.Context.Config.Navigation, f.Context.Config.Arena, f.Context.Config.enemyCount,
                f.Context.EntityLookup, f.Context.Paths, f.Context.ObstacleGridLookup), GameSession.FixedStep);
            Assert.That(f.Position(enemy).x, Is.GreaterThan(2f));
        }

        private sealed class Fixture : IDisposable {
            public readonly GameConfig Authoring;
            public readonly GameContext Context;
            public readonly World World;
            public readonly EntityFactory Factory;
            public Entity Player => this.Context.EntityLookup.player;
            public Entity State => this.Context.EntityLookup.gameState;
            public Fixture(Action<GameConfig> configure = null) {
                this.Authoring = ScriptableObject.CreateInstance<GameConfig>();
                this.Authoring.enemyTypes = new[] { new EnemyAuthoring(), Heavy() };
                this.Authoring.obstaclePositions = Array.Empty<Vector3>();
                configure?.Invoke(this.Authoring);
                this.Context = new GameContext(this.Authoring.CreateSimulationSettings());
                this.World = World.Create("Enemy type tests");
                this.World.UpdateByUnity = false;
                this.Factory = new EntityFactory(this.World, this.Context.Config, this.Context.NavigationRandom, this.Context.Stats, this.Context.Projectiles, this.Context.Weapons);
                this.Context.EntityLookup.gameState = this.Factory.CreateGameStateEntity();
                this.Context.EntityLookup.player = this.Factory.Players.Spawn(Vector3.zero);
                this.World.GetStash<GameStateComponent>().Get(this.State).phase = SessionPhase.Combat;
                this.World.Commit();
            }
            public Entity Spawn(int type, Vector3 position) => this.Factory.Enemies.Spawn(position, this.Context.Config.EnemyTypes[type]);
            public Vector3 Position(Entity entity) => this.World.GetStash<PositionComponent>().Get(entity).value;
            public void Initialize(ISystem system) { system.World = this.World; system.OnAwake(); this.World.Commit(); }
            public void Run(ISystem system, float dt = 0f) { this.Initialize(system); system.OnUpdate(dt); this.World.Commit(); system.Dispose(); }
            public void RebuildIndex() => this.Run(new EnemySpatialIndexSystem(this.Context.EnemySpatialIndex));
            public void Dispose() { this.World.Dispose(); this.Context.Dispose(); Object.DestroyImmediate(this.Authoring); }
        }
    }
}

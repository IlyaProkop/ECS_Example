using System;
using Game.Config;
using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.ECS.Spawning;
using Game.ECS.Systems;
using NUnit.Framework;
using Scellecs.Morpeh;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class SpawnerTests {
        private World world;
        private GameConfig authoring;
        private GameContext context;
        private EntityFactory factory;
        [SetUp] public void SetUp() {
            this.authoring = ScriptableObject.CreateInstance<GameConfig>();
            this.authoring.enemyCount = 2;
            this.authoring.obstaclePositions = Array.Empty<Vector3>();
            this.context = new GameContext(this.authoring.CreateSimulationSettings());
            this.world = World.Create("Spawner tests");
            this.world.UpdateByUnity = false;
            this.factory = new EntityFactory(this.world, this.context.Config, this.context.NavigationRandom, this.context.Stats, this.context.Projectiles, this.context.Weapons);
            this.context.EntityLookup.gameState = this.factory.CreateGameStateEntity();
            this.context.EntityLookup.player = this.factory.Players.Spawn(Vector3.zero);
            this.world.GetStash<GameStateComponent>().Get(this.context.EntityLookup.gameState).phase = SessionPhase.Combat;
            this.world.Commit();
        }
        [TearDown] public void TearDown() {
            this.world.Dispose();
            this.context.Dispose();
            Object.DestroyImmediate(this.authoring);
        }

        [Test]
        public void ActorIdsStayUniqueAcrossSpawnersAndEntityReuse() {
            var actors = this.world.GetStash<ActorComponent>();
            var playerId = actors.Get(this.context.EntityLookup.player).id;
            var enemy = this.factory.Enemies.Spawn(Vector3.right, this.context.Config.EnemyTypes[0]);
            var enemyId = actors.Get(enemy).id;
            var request = new ProjectileSpawn(Vector3.zero, Vector3.right, 10f, 20f, 0.1f, 2f, default);
            var projectile = this.factory.Projectiles.Spawn(request);
            var projectileId = actors.Get(projectile).id;
            this.world.RemoveEntity(enemy);
            this.world.RemoveEntity(projectile);
            this.world.Commit();
            var coin = this.factory.Coins.Spawn(Vector3.left);
            var replacement = this.factory.Enemies.Spawn(Vector3.forward, this.context.Config.EnemyTypes[0]);
            Assert.That(new[] { playerId, enemyId, projectileId, actors.Get(coin).id, actors.Get(replacement).id },
                Is.EqualTo(new[] { 1, 2, 3, 4, 5 }), "Presentation IDs must not alias after Morpeh reuses entity storage.");
            Assert.That(this.world.GetStash<EnemyPathComponent>().Get(replacement).slot, Is.EqualTo(1));
            Assert.That(this.world.GetStash<PositionComponent>().Get(coin).value.y, Is.EqualTo(this.authoring.coinRadius));
        }

        [Test]
        public void CapacityFailureDoesNotCreatePartialActorOrConsumeIdentityAndRandomState() {
            var first = this.factory.Enemies.Spawn(Vector3.right, this.context.Config.EnemyTypes[0]);
            this.factory.Enemies.Spawn(Vector3.left, this.context.Config.EnemyTypes[0]);
            // Destruction does not refund finite-wave navigation slots.
            this.world.RemoveEntity(first);
            this.world.Commit();
            var actors = this.world.Filter.With<ActorComponent>().Build();
            var beforeCount = actors.GetLengthSlow();
            Assert.Throws<InvalidOperationException>(() => this.factory.Enemies.Spawn(Vector3.forward, this.context.Config.EnemyTypes[0]));
            this.world.Commit();
            Assert.That(actors.GetLengthSlow(), Is.EqualTo(beforeCount));
            var coin = this.factory.Coins.Spawn(Vector3.zero);
            Assert.That(this.world.GetStash<ActorComponent>().Get(coin).id, Is.EqualTo(4));
            var expected = new System.Random(unchecked(this.authoring.randomSeed + 155921));
            if (this.authoring.enemyPathRepathInterval > 0f) { expected.NextDouble(); expected.NextDouble(); }
            Assert.That(this.context.NavigationRandom.NextDouble(), Is.EqualTo(expected.NextDouble()));
        }

        [Test]
        public void EnemyVariantUsesExistingPlacementAndWavePolicy() {
            var variant = new ArmoredSpawner(this.factory.Enemies, this.context.Stats);
            var system = new EnemySpawnSystem(this.context.Config, this.context.EntityLookup, this.context.SpawnRandom, variant, this.context.ObstacleGridLookup) { World = this.world };
            system.OnAwake();
            this.world.Commit();
            for (var i = 0; i < 20; i++) { system.OnUpdate(1f); this.world.Commit(); }
            system.Dispose();
            Assert.That(variant.Count, Is.EqualTo(2));
            Assert.That(this.world.GetStash<GameStateComponent>().Get(this.context.EntityLookup.gameState).spawnedEnemies, Is.EqualTo(2));
            foreach (var enemy in this.world.Filter.With<EnemyTag>().Build()) {
                Assert.That(this.world.GetStash<DefenseComponent>().Get(enemy).armor, Is.EqualTo(75f));
                Assert.That(this.world.GetStash<HealthComponent>().Get(enemy).current, Is.EqualTo(this.authoring.enemyTypes[0].maxHealth));
            }
        }

        [Test]
        public void ProjectileRequestRetainsSourceSnapshotAcrossSpawnerStructuralChanges() {
            var player = this.context.EntityLookup.player;
            this.factory.Enemies.Spawn(Vector3.right * 5f, this.context.Config.EnemyTypes[0]);
            this.world.GetStash<CombatInputComponent>().Get(player).attackHeld = true;
            var original = this.world.GetStash<DamageSourceComponent>().Get(player);
            var variant = new InterceptingProjectileSpawner(this.factory.Projectiles, () => {
                this.world.GetStash<DamageSourceComponent>().Set(player,
                    new DamageSourceComponent { owner = player.ID, team = Team.Enemy, criticalMultiplier = 99f });
                // Force structural churn inside the extension boundary.
                for (var i = 0; i < 1024; i++) this.factory.Coins.Spawn(Vector3.zero);
            });
            var index = new EnemySpatialIndexSystem(this.context.EnemySpatialIndex) { World = this.world };
            index.OnAwake(); this.world.Commit();
            index.OnUpdate(0f); SharedWeaponTestRunner.Fire(this.world, this.context, variant); this.world.Commit();
            index.Dispose();
            var source = this.world.GetStash<DamageSourceComponent>().Get(variant.Created);
            Assert.That(source.owner, Is.EqualTo(original.owner));
            Assert.That(source.team, Is.EqualTo(original.team));
            Assert.That(source.criticalChance, Is.EqualTo(original.criticalChance));
            Assert.That(source.criticalMultiplier, Is.EqualTo(original.criticalMultiplier));
            Assert.That(this.world.GetStash<WeaponComponent>().Get(player).cooldown, Is.EqualTo(this.authoring.projectileInterval));
            Assert.That(this.world.GetStash<BattleStatisticsComponent>().Get(this.context.EntityLookup.gameState).value.attacks, Is.EqualTo(1));
        }

        private sealed class ArmoredSpawner : IEnemySpawner {
            private readonly IEnemySpawner inner;
            private readonly Game.ECS.Stats.StatStorage stats;
            public int Count { get; private set; }
            public ArmoredSpawner(IEnemySpawner inner, Game.ECS.Stats.StatStorage stats) { this.inner = inner; this.stats = stats; }
            public Entity Spawn(Vector3 position, EnemyDefinition definition) {
                var entity = this.inner.Spawn(position, definition);
                this.stats.SetBase(entity, Game.Domain.Stats.StatIds.Armor, 75f);
                this.stats.RecalculateDirty();
                this.Count++;
                return entity;
            }
        }
        private sealed class InterceptingProjectileSpawner : IProjectileSpawner {
            private readonly IProjectileSpawner inner;
            private readonly Action beforeSpawn;
            public Entity Created { get; private set; }
            public InterceptingProjectileSpawner(IProjectileSpawner inner, Action beforeSpawn) { this.inner = inner; this.beforeSpawn = beforeSpawn; }
            public Entity Spawn(in ProjectileSpawn request) {
                this.beforeSpawn();
                return this.Created = this.inner.Spawn(request);
            }
        }
    }
}

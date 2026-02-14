using System;
using Game.Config;
using Game.Domain;
using Game.Domain.Abilities;
using Game.ECS.Abilities;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.ECS.Systems;
using Game.Input;
using NUnit.Framework;
using Scellecs.Morpeh;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class AbilityModuleTests {
        [Test]
        public void RegisteredEffectComposesWithAreaDamageAndUsesTheCommonDamagePipeline() {
            var definition = new AbilityDefinition(12, 1f, new DirectHit(14f), new AreaDamageDefinition(3f, 20f));
            using var fixture = new Fixture(definition, new DamagePipeline(new HalfDamage()));
            var near = fixture.factory.Enemies.Spawn(Vector3.right, fixture.context.Config.EnemyTypes[0]);
            var far = fixture.factory.Enemies.Spawn(Vector3.right * 8f, fixture.context.Config.EnemyTypes[0]);
            var registry = fixture.Registry();
            DirectHitExecutor executor = null;
            registry.Register<DirectHit>(effect => executor = new DirectHitExecutor(fixture.context.DamageRequests, far, effect.amount));
            fixture.Start(registry);
            fixture.Tick();
            Assert.That(fixture.Health(near), Is.EqualTo(90f));
            Assert.That(fixture.Health(far), Is.EqualTo(93f));
            Assert.That(fixture.Statistics.damageDealt, Is.EqualTo(17f));
            Assert.That(fixture.Statistics.abilityUses, Is.EqualTo(1));
            fixture.Tick(0.1f);
            Assert.That(executor.executions, Is.EqualTo(1), "The new executor shares the existing cooldown.");
            fixture.Tick(1f);
            Assert.That(executor.executions, Is.EqualTo(2));
            fixture.Dispose();
            Assert.That(executor.disposed, Is.True);
        }

        [Test]
        public void CompoundEffectCapacityIncludesEveryHitRatherThanAssumingOneArea() {
            var ability = new AbilityDefinition(1, 5f,
                new AreaDamageDefinition(10f, 3f), new AreaDamageDefinition(10f, 4f));
            using var fixture = new Fixture(ability);
            for (var i = 0; i < fixture.context.Config.enemyCount; i++) fixture.factory.Enemies.Spawn(Vector3.right * (i + 1), fixture.context.Config.EnemyTypes[0]);
            fixture.Start(fixture.Registry());
            fixture.Tick();
            Assert.That(fixture.context.DamageRequests.PeakCount, Is.EqualTo(6));
            Assert.That(fixture.context.Config.DamageCapacity, Is.EqualTo(9 + fixture.context.Config.ProjectileCapacity));
            Assert.That(fixture.Statistics.damageDealt, Is.EqualTo(21f));
            Assert.That(fixture.Statistics.abilityUses, Is.EqualTo(1));
        }

        [Test]
        public void EmptyCastConsumesCooldownAndFactsAreConsumedOnce() {
            using var fixture = new Fixture(BuiltInAbilities.CreateShockwave());
            fixture.Start(fixture.Registry());
            fixture.Tick();
            fixture.Tick(0.1f);
            Assert.That(fixture.Statistics.abilityUses, Is.EqualTo(1));
            Assert.That(fixture.Statistics.damageDealt, Is.Zero);
            Assert.That(fixture.context.AbilityCasts.Count, Is.Zero);
            Assert.That(fixture.context.AbilityEvents.Count, Is.EqualTo(1));
            Assert.That(fixture.world.GetStash<AbilityComponent>().Get(fixture.context.EntityLookup.player).remainingCooldown, Is.EqualTo(4.9f).Within(0.001f));
        }

        [Test]
        public void DeadCasterCannotActivateAndEverySuccessfulStepRetainsItsEvent() {
            using var fixture = new Fixture(BuiltInAbilities.CreateShockwave(cooldown: 0.001f));
            fixture.Start(fixture.Registry());
            for (var i = 0; i < GameSession.MaxStepsPerFrame; i++) fixture.Tick();
            Assert.That(fixture.context.AbilityEvents.Count, Is.EqualTo(8));
            for (var i = 0; i < 8; i++) Assert.That(fixture.context.AbilityEvents.Items[i].sequence, Is.EqualTo(i + 1));
            fixture.world.GetStash<HealthComponent>().Get(fixture.context.EntityLookup.player).current = 0f;
            fixture.Tick();
            Assert.That(fixture.Statistics.abilityUses, Is.EqualTo(8));
        }

        [Test]
        public void PublicEventBatchCanBeCopiedTwiceAndExpiresOnTheNextHostUpdate() {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try {
                config.enemyTypes[0].damagePerSecond = 0f;
                config.obstaclePositions = Array.Empty<Vector3>();
                using var session = new GameSession(config.CreateSimulationSettings());
                for (var i = 0; i < 80; i++) session.TickUpdate(GameSession.FixedStep, new PlayerInputFrame { move = Vector2.up });
                session.TickUpdate(GameSession.FixedStep, new PlayerInputFrame { abilityPressed = true });
                var events = new AbilityCastEvent[session.AbilityEventCapacity];
                Assert.That(session.CopyAbilityEvents(events), Is.EqualTo(1));
                var sequence = events[0].sequence;
                events[0] = default;
                Assert.That(session.CopyAbilityEvents(events), Is.EqualTo(1));
                Assert.That(events[0].sequence, Is.EqualTo(sequence));
                Assert.Throws<ArgumentException>(() => session.CopyAbilityEvents(Array.Empty<AbilityCastEvent>()));
                session.TickUpdate(0f, default);
                Assert.That(session.CopyAbilityEvents(events), Is.Zero);
                session.Dispose();
                Assert.Throws<ObjectDisposedException>(() => session.CopyAbilityEvents(events));
            } finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void MissingRegistrationFailsBeforeActivationAndDisposesPartialCompilation() {
            var first = new DirectHitExecutor(null, null, 0f);
            var registry = new AbilityEffectRegistry();
            registry.Register<DirectHit>(_ => first);
            Assert.Throws<InvalidOperationException>(() => registry.Register<DirectHit>(_ => first));
            Assert.Throws<InvalidOperationException>(() => registry.Compile(new AbilityDefinition(1, 1f, new DirectHit(1f), new AreaDamageDefinition(1f, 1f))));
            Assert.That(first.executions, Is.Zero);
            Assert.That(first.disposed, Is.True);
        }

        [Test]
        public void AuthoringChangesAndEffectArrayReplacementCannotChangeTheCompiledDefinition() {
            var asset = ScriptableObject.CreateInstance<AbilityAsset>();
            try {
                var effect = (AreaDamageAuthoring)asset.effects[0];
                var definition = asset.CreateDefinition();
                effect.damage = 999f;
                asset.cooldown = 99f;
                asset.effects[0] = null;
                Assert.That(definition.Cooldown, Is.EqualTo(5f));
                Assert.That(((AreaDamageDefinition)definition.Effects[0]).Damage, Is.EqualTo(65f));
                Assert.Throws<InvalidOperationException>(() => asset.CreateDefinition());
                Assert.Throws<ArgumentOutOfRangeException>(() => new AbilityDefinition(1, float.NaN, definition.Effects[0]));
            } finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void MultipleSlotsAndEnemyCasterUseIndependentCooldownsAndHostileTargets() {
            var secondary = ScriptableObject.CreateInstance<AbilityAsset>();
            secondary.id = 42; secondary.cooldown = 2f;
            secondary.effects = new AbilityEffectAuthoring[] { new AreaDamageAuthoring { radius = 5f, damage = 7f } };
            try {
                using var f = new Fixture(new AbilityDefinition(41, 1f, new AreaDamageDefinition(5f, 3f)), configure: config => {
                    config.playerArmor = 0f;
                    config.additionalPlayerAbilities = new[] { secondary };
                    config.enemyTypes[0].abilities = new[] { secondary };
                });
                var enemy = f.factory.Enemies.Spawn(Vector3.right * 2f, f.context.Config.EnemyTypes[0]);
                var allyOfEnemy = f.factory.Enemies.Spawn(Vector3.left * 2f, f.context.Config.EnemyTypes[0]);
                f.Start(f.Registry()); f.world.Commit();
                var player = f.context.EntityLookup.player;
                Assert.That(f.context.AbilityCasts.Capacity, Is.EqualTo(5));
                Assert.That(AbilityEquipment.Request(f.world, player, 1), Is.True);
                AbilityEquipment.Request(f.world, player, 1); // Repeated producers still mean one activation.
                AbilityEquipment.Request(f.world, enemy, 0);
                Assert.That(AbilityEquipment.Request(f.world, enemy, 1), Is.False);
                f.Tick();
                Assert.That(f.Health(enemy), Is.EqualTo(90f));
                Assert.That(f.Health(allyOfEnemy), Is.EqualTo(90f), "Enemy area damage must not hit its allies.");
                Assert.That(f.Health(player), Is.EqualTo(93f));
                Assert.That(f.Statistics.abilityUses, Is.EqualTo(2), "Player statistics exclude the enemy cast.");
                Assert.That(f.context.AbilityEvents.Items[2].abilityId, Is.EqualTo(42));
                AbilityEquipment.Request(f.world, player, 1);
                AbilityEquipment.Request(f.world, enemy, 0);
                f.Tick(1f);
                Assert.That(f.Statistics.abilityUses, Is.EqualTo(3));
                Assert.That(f.Health(player), Is.EqualTo(93f));
                Assert.That(f.world.GetStash<AbilityComponent>().Get(player).Get(1).remainingCooldown, Is.EqualTo(1f));
                f.world.GetStash<HealthComponent>().Get(enemy).current = 0f;
                AbilityEquipment.Request(f.world, enemy, 0);
                f.Tick(1f);
                Assert.That(f.Health(player), Is.EqualTo(93f));
                Assert.That(f.world.GetStash<AbilityComponent>().Get(enemy).requestedSlots, Is.Zero);
            } finally { Object.DestroyImmediate(secondary); }
        }

        [Test]
        public void FourSlotEdgesAccumulateUntilStepAndDoNotRepeatAfterConsumption() {
            var secondary = ScriptableObject.CreateInstance<AbilityAsset>();
            secondary.id = 72; secondary.cooldown = 1f;
            secondary.effects = new AbilityEffectAuthoring[] { new AreaDamageAuthoring { radius = 1f, damage = 0f } };
            try {
                using var f = new Fixture(new AbilityDefinition(71, 1f, new AreaDamageDefinition(1f, 0f)), configure: config =>
                    config.additionalPlayerAbilities = new[] { secondary, secondary, secondary });
                f.Start(f.Registry());
                var input = new SimulationInput();
                input.Submit(new PlayerInputFrame { abilitySlotsPressed = 3 });
                input.Submit(new PlayerInputFrame { abilitySlotsPressed = 12 });
                var owner = f.context.EntityLookup.player;
                f.world.GetStash<CombatInputComponent>().Set(owner, new CombatInputComponent { abilitySlotsPressed = input.ReadFrame().abilitySlotsPressed });
                f.Tick();
                Assert.That(f.Statistics.abilityUses, Is.EqualTo(4));
                Assert.That(f.context.AbilityCasts.PeakCount, Is.EqualTo(4));
                for (var i = 0; i < 4; i++) Assert.That(f.world.GetStash<AbilityComponent>().Get(owner).Get(i).remainingCooldown, Is.EqualTo(1f));
                input.CompleteStep();
                f.world.GetStash<CombatInputComponent>().Set(owner, new CombatInputComponent { abilitySlotsPressed = input.ReadFrame().abilitySlotsPressed });
                f.Tick(2f);
                Assert.That(f.Statistics.abilityUses, Is.EqualTo(4));
            } finally { Object.DestroyImmediate(secondary); }
        }

        [Test]
        public void PlayerSlotSnapshotReflectsUiCommandAndSurvivesLaterStepsAsACopy() {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var secondary = ScriptableObject.CreateInstance<AbilityAsset>();
            secondary.id = 82; secondary.cooldown = 3f;
            secondary.effects = new AbilityEffectAuthoring[] { new AreaDamageAuthoring { radius = 1f, damage = 0f } };
            config.additionalPlayerAbilities = new[] { secondary };
            config.obstaclePositions = Array.Empty<Vector3>();
            config.enemyTypes[0].damagePerSecond = 0f;
            try {
                using var session = new GameSession(config.CreateSimulationSettings());
                Assert.That(session.Snapshot.abilities.Count, Is.EqualTo(2));
                Assert.That(session.Snapshot.abilities[1].CanActivate, Is.False);
                Assert.That(session.TryActivateAbility(1), Is.False);
                for (var i = 0; i < 80; i++) session.TickUpdate(GameSession.FixedStep, new PlayerInputFrame { move = Vector2.up });
                Assert.That(session.Snapshot.abilities[1].CanActivate, Is.True);
                Assert.That(session.TryActivateAbility(1), Is.True);
                session.TickUpdate(GameSession.FixedStep, default);
                var frozen = session.Snapshot.abilities;
                Assert.That(frozen[0].Remaining, Is.Zero);
                Assert.That(frozen[1].Id, Is.EqualTo(82));
                Assert.That(frozen[1].Remaining, Is.EqualTo(3f));
                Assert.That(frozen[1].CanActivate, Is.False);
                session.TickUpdate(GameSession.FixedStep, default);
                Assert.That(session.Snapshot.abilities[1].Remaining, Is.LessThan(frozen[1].Remaining));
            } finally { Object.DestroyImmediate(config); Object.DestroyImmediate(secondary); }
        }

        [Test]
        public void PublicEventCapacityCanCopyMoreThanEightEnemyCastsInOneHostUpdate() {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var ability = ScriptableObject.CreateInstance<AbilityAsset>();
            ability.id = 91;
            ability.effects = new AbilityEffectAuthoring[] { new AreaDamageAuthoring { radius = 100f, damage = 0f } };
            config.enemyCount = 12; config.enemySpawnInterval = 0.1f;
            config.obstaclePositions = Array.Empty<Vector3>();
            foreach (var type in config.enemyTypes) { type.damagePerSecond = 0f; type.abilities = new[] { ability }; }
            try {
                using var session = new GameSession(config.CreateSimulationSettings());
                for (var i = 0; i < 300; i++) session.TickUpdate(GameSession.FixedStep, new PlayerInputFrame { move = i < 80 ? Vector2.up : Vector2.zero });
                var actors = new ActorView[session.ViewCapacity];
                var count = session.CopyActors(actors);
                var requested = 0;
                for (var i = 0; i < count; i++) if (actors[i].kind == ActorKind.Enemy) {
                    Assert.That(session.TryRequestAbility(actors[i].id, 0), Is.True); requested++;
                }
                Assert.That(requested, Is.EqualTo(12));
                session.TickUpdate(GameSession.FixedStep, default);
                var events = new AbilityCastEvent[session.AbilityEventCapacity];
                Assert.That(session.CopyAbilityEvents(events), Is.EqualTo(12));
                Assert.That(events[11].abilityId, Is.EqualTo(91));
                Assert.That(session.CopyAbilityEvents(events), Is.EqualTo(12));
                session.TickUpdate(0f, default);
                Assert.That(session.CopyAbilityEvents(events), Is.Zero);
            } finally { Object.DestroyImmediate(config); Object.DestroyImmediate(ability); }
        }

        [Test]
        public void TenThousandEnemyAreaAbilitiesHaveLinearDamageReserve() {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var ability = ScriptableObject.CreateInstance<AbilityAsset>();
            ability.id = 92;
            ability.effects = new AbilityEffectAuthoring[] { new AreaDamageAuthoring { radius = 100f, damage = 1f } };
            config.enemyCount = 10000;
            foreach (var type in config.enemyTypes) type.abilities = new[] { ability };
            try {
                var settings = config.CreateSimulationSettings();
                Assert.That(settings.DamageCapacity, Is.LessThan(40000));
                Assert.That(CombatBufferBudget.EstimateBytes(settings.DamageCapacity, settings.AbilityCastCapacity), Is.LessThan(CombatBufferBudget.MaxBytes));
                Assert.Throws<ArgumentException>(() => config.CreateSimulationSettings(new AbilityDefinition(93, 1f, new ExcessiveDamage())));
            } finally { Object.DestroyImmediate(config); Object.DestroyImmediate(ability); }
        }
        private sealed class ExcessiveDamage : AbilityEffectDefinition {
            public override int GetDamageCapacity(int targets) => 1000000;
        }

        [Test]
        public void EnemyAreaQueryInspectsOnlyPlayerAndRejectsDeadOrOutOfRangeTargets() {
            using var f = new Fixture(BuiltInAbilities.CreateShockwave());
            for (var i = 0; i < 3; i++) f.factory.Enemies.Spawn(Vector3.right * (i + 1), f.context.Config.EnemyTypes[0]);
            var query = new HostileAreaQuery(f.world, f.context.EnemySpatialIndex, f.context.ObstacleGridLookup);
            f.world.Commit();
            var visitor = new CountingTargetVisitor();
            for (var i = 0; i < 100; i++) Assert.That(query.Visit(Team.Enemy, Vector3.zero, 5f, ref visitor), Is.EqualTo(1));
            Assert.That(visitor.count, Is.EqualTo(100));
            query.Visit(Team.Enemy, Vector3.right * 10f, 1f, ref visitor);
            f.world.GetStash<HealthComponent>().Get(f.context.EntityLookup.player).current = 0;
            query.Visit(Team.Enemy, Vector3.zero, 5f, ref visitor);
            query.Visit(Team.Neutral, Vector3.zero, 5f, ref visitor);
            Assert.That(visitor.count, Is.EqualTo(100));
        }
        private struct CountingTargetVisitor : IAreaTargetVisitor {
            public int count;
            public void Visit(Entity target) => this.count++;
        }

        private sealed class Fixture : IDisposable {
            private bool disposed;
            public readonly World world;
            public readonly GameContext context;
            public readonly EntityFactory factory;
            public BattleStatistics Statistics => this.world.GetStash<BattleStatisticsComponent>().Get(this.context.EntityLookup.gameState).value;
            public Fixture(AbilityDefinition ability, DamagePipeline damage = null, Action<GameConfig> configure = null) {
                var config = ScriptableObject.CreateInstance<GameConfig>();
                config.enemyCount = 3;
                config.enemyTypes[0].maxHealth = 100f;
                config.criticalChance = 0f;
                config.obstaclePositions = Array.Empty<Vector3>();
                configure?.Invoke(config);
                try { this.context = new GameContext(config.CreateSimulationSettings(ability), damage); }
                finally { Object.DestroyImmediate(config); }
                this.world = World.Create("Ability module test");
                this.world.UpdateByUnity = false;
                this.factory = new EntityFactory(this.world, this.context.Config, this.context.NavigationRandom, this.context.Stats, this.context.Projectiles, this.context.Weapons);
                this.context.EntityLookup.gameState = this.factory.CreateGameStateEntity();
                this.context.EntityLookup.player = this.factory.Players.Spawn(Vector3.zero);
                this.world.GetStash<GameStateComponent>().Get(this.context.EntityLookup.gameState).phase = SessionPhase.Combat;
                this.world.GetStash<CombatInputComponent>().Get(this.context.EntityLookup.player).abilityPressed = true;
            }
            public AbilityEffectRegistry Registry() => AbilityModule.CreateRegistry(this.world, this.context.EnemySpatialIndex,
                this.context.ObstacleGridLookup, this.context.DamageRequests, this.context.Stats);
            public void Start(AbilityEffectRegistry registry) {
                var systems = this.world.CreateSystemsGroup();
                systems.AddSystem(new EnemySpatialIndexSystem(this.context.EnemySpatialIndex));
                AbilityModule.Install(system => systems.AddSystem(system), this.context.EntityLookup, this.context.Config.AbilityDefinitions,
                    registry, this.context.AbilityCasts, this.context.AbilityEvents);
                systems.AddSystem(new ApplyDamageEventsSystem(this.context.EntityLookup, this.context.DamageRequests,
                    this.context.DamageApplied, this.context.DamagePipeline, this.context.CombatRandom));
                systems.AddSystem(new CombatStatisticsSystem(this.context.EntityLookup, this.context.DamageApplied));
                this.world.AddSystemsGroup(0, systems);
            }
            public void Tick(float dt = GameSession.FixedStep) => this.world.Update(dt);
            public float Health(Entity entity) => this.world.GetStash<HealthComponent>().Get(entity).current;
            public void Dispose() {
                if (this.disposed) return;
                this.disposed = true;
                try { this.world.Dispose(); }
                finally { this.context.Dispose(); }
            }
        }
        private sealed class DirectHit : AbilityEffectDefinition {
            public readonly float amount;
            public DirectHit(float amount) => this.amount = amount;
            public override int GetDamageCapacity(int targetCapacity) => 1;
        }
        private sealed class DirectHitExecutor : IAbilityEffect {
            private readonly FrameBuffer<DamageRequest> requests;
            private readonly Entity target;
            private readonly float amount;
            public int executions;
            public bool disposed;
            public DirectHitExecutor(FrameBuffer<DamageRequest> requests, Entity target, float amount) {
                this.requests = requests; this.target = target; this.amount = amount;
            }
            public void Execute(in AbilityCast cast) {
                this.executions++;
                this.requests.Add(new DamageRequest { target = this.target, source = cast.source, value = this.amount, kind = DamageKind.Area });
            }
            public void Dispose() => this.disposed = true;
        }
        private sealed class HalfDamage : IDamageModifier {
            public void Apply(ref DamageCalculation damage) => damage.amount *= 0.5f;
        }
    }
}

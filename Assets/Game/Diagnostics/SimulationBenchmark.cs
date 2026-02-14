using System;
using System.Diagnostics;
using System.IO;
using Game.Config;
using Game.Domain;
using Game.Domain.Abilities;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.Input;
using Scellecs.Morpeh;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Diagnostics {
    // Opt-in reproducible CPU benchmark in the same non-development player as the game.
    public static class SimulationBenchmark {
        private const int WarmupTicks = 180;
        private const int SampleTicks = 600;
        private static bool allocationCounterSupported;
        private static long allocationProbeBytes;
        private static readonly System.Collections.Generic.IComparer<ActorView> ActorOrder =
            System.Collections.Generic.Comparer<ActorView>.Create((a, b) => a.id.CompareTo(b.id));

        private static void CalibrateAllocationCounter() {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[65536];
            allocationProbeBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(probe);
            allocationCounterSupported = allocationProbeBytes >= 65536;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RunIfRequested() {
            var arguments = Environment.GetCommandLineArgs();
            if (Array.IndexOf(arguments, "-arena-benchmark") < 0) return;
            var outputIndex = Array.IndexOf(arguments, "-arena-benchmark-output");
            var output = outputIndex >= 0 && outputIndex + 1 < arguments.Length ? arguments[outputIndex + 1] :
                Path.Combine(Application.persistentDataPath, "simulation-benchmark.json");
            try {
                CalibrateAllocationCounter();
                var profile = Array.IndexOf(arguments, "-arena-benchmark-profile") >= 0;
                var mixed = Array.IndexOf(arguments, "-arena-benchmark-mixed-enemies") >= 0;
                var pooling = Array.IndexOf(arguments, "-arena-benchmark-no-projectile-pool") < 0;
                var report = new Report {
                    systemProfiling = profile,
                    mixedEnemies = mixed,
                    projectilePooling = pooling,
                    allocationCounterSupported = allocationCounterSupported,
                    allocationProbeBytes = allocationProbeBytes,
                    unity = Application.unityVersion, platform = Application.platform.ToString(),
                    cpu = SystemInfo.processorType, development = UnityEngine.Debug.isDebugBuild,
                    warmupTicks = WarmupTicks, sampleTicks = SampleTicks,
                    cases = new Case[6]
                };
                var sizes = new[] { 12, 128, 512 };
                for (var i = 0; i < sizes.Length; i++) {
                    report.cases[i * 2] = Measure(sizes[i], false, profile, mixed, pooling);
                    report.cases[i * 2 + 1] = Measure(sizes[i], true, profile, mixed, pooling);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                Application.Quit(0);
            } catch (Exception exception) {
                UnityEngine.Debug.LogException(exception);
                Application.Quit(1);
            }
        }

        private static Case Measure(int count, bool attacks, bool profile, bool mixed, bool pooling) {
            var times = new double[SampleTicks];
            var timings = profile ? new SystemTimings(SampleTicks) : null;
            var config = mixed ? Object.Instantiate(Resources.Load<GameConfig>("GameConfig")) : ScriptableObject.CreateInstance<GameConfig>();
            config.enemyCount = count;
            foreach (var type in config.enemyTypes) type.maxHealth = 10000f;
            config.playerMaxHealth = mixed ? 1000000f : 10000f;
            config.coinDropChance = 0f;
            var settings = config.CreateSimulationSettings();
            var views = new ActorView[settings.ViewCapacity];
            var coldBytes = GC.GetAllocatedBytesForCurrentThread();
            var coldStart = Stopwatch.GetTimestamp();
            using var context = new GameContext(settings);
            using var world = World.Create("Simulation Benchmark");
            world.UpdateByUnity = false;
            try {
                var factory = new EntityFactory(world, context.Config, context.NavigationRandom, context.Stats, context.Projectiles, context.Weapons,
                    pooling ? context.ProjectilePool : null);
                context.EntityLookup.gameState = factory.CreateGameStateEntity();
                context.EntityLookup.player = factory.Players.Spawn(Vector3.zero);
                // Identical deterministic population, finite arena and original four obstacles.
                var random = new System.Random(7283);
                for (var i = 0; i < count; i++) {
                    var definition = settings.EnemyForSpawn(i);
                    var placement = new Game.Spatial.ActorMotionQuery(settings.arenaHalfSize, definition.Radius, true, context.ObstacleGridLookup.AsNative());
                    Vector3 position;
                    do {
                        position = new Vector3((float)random.NextDouble() * 25f - 12.5f, 0f, (float)random.NextDouble() * 17f - 8.5f);
                    } while (placement.IsBlocked(position));
                    factory.Enemies.Spawn(position, definition);
                }
                ref var state = ref world.GetStash<GameStateComponent>().Get(context.EntityLookup.gameState);
                state.phase = SessionPhase.Combat;
                state.spawnedEnemies = count;
                new GameplayInstaller(context, factory).Install(world, timings == null ? null : timings.Decorate);
                var reader = new SessionReader(world, context.EntityLookup, count, context.Stats);
                world.Update(0f);
                var result = new Case {
                    enemies = count, attacks = attacks,
                    coldMilliseconds = Milliseconds(Stopwatch.GetTimestamp() - coldStart),
                    coldManagedBytes = allocationCounterSupported ? GC.GetAllocatedBytesForCurrentThread() - coldBytes : -1
                };
                var abilityEvents = new AbilityCastEvent[GameSession.MaxStepsPerFrame];
                for (var i = 0; i < WarmupTicks; i++) Tick(world, context, reader, views, abilityEvents, i, attacks);
                if (timings != null) timings.Recording = true;
                var trace = 14695981039346656037UL;
                var canonicalTrace = trace;
                var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var collectionsBefore = GC.CollectionCount(0);
                for (var i = 0; i < SampleTicks; i++) {
                    var started = Stopwatch.GetTimestamp();
                    var actors = Tick(world, context, reader, views, abilityEvents, i + WarmupTicks, attacks);
                    times[i] = Milliseconds(Stopwatch.GetTimestamp() - started);
                    // Outside the timed interval. Detect changed ordering, trajectories and health.
                    HashActors(ref trace, views, actors);
                    // Compare identities and values separately from unspecified filter order.
                    Array.Sort(views, 0, actors, ActorOrder);
                    HashActors(ref canonicalTrace, views, actors);
                }
                result.sampleManagedBytes = allocationCounterSupported ? GC.GetAllocatedBytesForCurrentThread() - allocatedBefore : -1;
                result.gen0Collections = GC.CollectionCount(0) - collectionsBefore;
                result.peakDamageRequests = context.DamageRequests.PeakCount;
                result.damageCapacity = settings.DamageCapacity;
                result.damageReceived = reader.ReadSnapshot().statistics.damageReceived;
                result.finalPhase = reader.ReadSnapshot().phase.ToString();
                if (reader.ReadSnapshot().phase != SessionPhase.Combat)
                    throw new InvalidOperationException("Benchmark ended combat before completing the measured workload.");
                result.actorTrace = trace.ToString("x16");
                result.canonicalActorTrace = canonicalTrace.ToString("x16");
                if (timings != null) { timings.Recording = false; result.systems = timings.Read(); }
                Array.Sort(times);
                result.medianMilliseconds = times[times.Length / 2];
                result.p95Milliseconds = times[(int)(times.Length * 0.95)];
                result.maxMilliseconds = times[times.Length - 1];
                return result;
            } finally { Object.Destroy(config); }
        }

        private static int Tick(World world, GameContext context, SessionReader reader, ActorView[] views, AbilityCastEvent[] abilityEvents, int tick, bool attacks) {
            // Rectangular movement input exercises navigation, contact, movement and snapshot copying.
            var side = (tick / 120) % 4;
            var move = side == 0 ? Vector2.right : side == 1 ? Vector2.up : side == 2 ? Vector2.left : Vector2.down;
            context.AbilityEvents.Clear();
            context.InputState.Frame = new PlayerInputFrame { move = move, attackHeld = attacks, abilityPressed = attacks && tick % 300 == 0 };
            world.Update(GameSession.FixedStep);
            reader.ReadSnapshot();
            var actors = reader.CopyActors(views);
            context.AbilityEvents.Items.CopyTo(abilityEvents);
            return actors;
        }
        private static void HashActors(ref ulong trace, ActorView[] views, int count) {
            Hash(ref trace, count);
            for (var i = 0; i < count; i++) {
                var actor = views[i];
                Hash(ref trace, actor.id); Hash(ref trace, (int)actor.kind);
                Hash(ref trace, BitConverter.SingleToInt32Bits(actor.position.x));
                Hash(ref trace, BitConverter.SingleToInt32Bits(actor.position.y));
                Hash(ref trace, BitConverter.SingleToInt32Bits(actor.position.z));
                Hash(ref trace, BitConverter.SingleToInt32Bits(actor.radius));
                Hash(ref trace, BitConverter.SingleToInt32Bits(actor.health));
            }
        }
        private static void Hash(ref ulong trace, int value) => trace = unchecked((trace ^ (uint)value) * 1099511628211UL);
        private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        [Serializable] private sealed class Report {
            public string unity, platform, cpu;
            public bool development, allocationCounterSupported, systemProfiling, mixedEnemies, projectilePooling;
            public long allocationProbeBytes;
            public int warmupTicks, sampleTicks;
            public Case[] cases;
        }
        [Serializable] private sealed class Case {
            public int enemies, gen0Collections;
            public bool attacks;
            public double coldMilliseconds, medianMilliseconds, p95Milliseconds, maxMilliseconds;
            public long coldManagedBytes, sampleManagedBytes;
            public int peakDamageRequests, damageCapacity;
            public float damageReceived;
            public string actorTrace;
            public string canonicalActorTrace;
            public string finalPhase;
            public SystemTimings.Entry[] systems;
        }
    }
}

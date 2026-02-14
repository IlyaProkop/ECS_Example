using System;
using System.Diagnostics;
using System.IO;
using Game.Config;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.ECS.Spawning;
using Game.ECS.Systems;
using Scellecs.Morpeh;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Diagnostics {
    // Same initialization and retirement systems, with only the allocator/recycler changed.
    public static class ProjectileLifecycleBenchmark {
        [Serializable] public sealed class Sample {
            public bool pooled, allocationCounterSupported;
            public int batchSize, cycles, shots, gen0Collections, preparedEntities;
            public long allocatedBytes, allocationEvents;
            public double preparationMilliseconds, medianBatchMilliseconds, p95BatchMilliseconds;
        }
        [Serializable] private sealed class Report {
            public string unity, platform, cpu;
            public bool development;
            public Sample[] samples;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RunIfRequested() {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-arena-projectile-benchmark") < 0) return;
            try {
                var outputIndex = Array.IndexOf(args, "-arena-benchmark-output");
                if (outputIndex < 0 || outputIndex + 1 == args.Length) throw new ArgumentException("Supply -arena-benchmark-output.");
                var report = new Report { unity = Application.unityVersion, platform = Application.platform.ToString(),
                    cpu = SystemInfo.processorType, development = UnityEngine.Debug.isDebugBuild, samples = new Sample[12] };
                var index = 0;
                for (var repeat = 0; repeat < 3; repeat++) foreach (var batch in new[] { 16, 128 }) {
                    report.samples[index++] = Measure(batch, repeat % 2 == 0);
                    report.samples[index++] = Measure(batch, repeat % 2 != 0);
                }
                var output = Path.GetFullPath(args[outputIndex + 1]); Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, JsonUtility.ToJson(report, true)); Application.Quit(0);
            } catch (Exception exception) { UnityEngine.Debug.LogException(exception); Application.Quit(1); }
        }
        public static Sample Measure(int batch, bool pooled) {
            const int cycles = 256;
            using var allocations = new AllocationMeter();
            var config = ScriptableObject.CreateInstance<GameConfig>(); config.projectileInterval = .02f;
            config.obstaclePositions = Array.Empty<Vector3>();
            var settings = config.CreateSimulationSettings();
            if (batch < 1 || batch > settings.ProjectileCapacity) { Object.DestroyImmediate(config); throw new ArgumentOutOfRangeException(nameof(batch)); }
            var start = Stopwatch.GetTimestamp();
            using var context = new GameContext(settings);
            using var world = World.Create("Projectile lifecycle benchmark"); world.UpdateByUnity = false;
            try {
                var pool = pooled ? context.ProjectilePool : null;
                var factory = new EntityFactory(world, settings, context.NavigationRandom, context.Stats, context.Projectiles, context.Weapons, pool);
                var destroy = new DestroyMarkedEntitiesSystem(context.Stats, pool) { World = world }; destroy.OnAwake();
                var retire = world.GetStash<DestroyTag>(); var entities = new Entity[batch]; var times = new double[cycles];
                var request = new ProjectileSpawn(Vector3.zero, Vector3.right, 16f, 25f, .2f, 4f, default);
                world.Commit();
                var preparation = Milliseconds(Stopwatch.GetTimestamp() - start);
                void Cycle() {
                    for (var i = 0; i < batch; i++) entities[i] = factory.Projectiles.Spawn(request);
                    world.Commit();
                    for (var i = 0; i < batch; i++) retire.Add(entities[i]);
                    world.Commit(); destroy.OnUpdate(0f); world.Commit();
                }
                for (var i = 0; i < 16; i++) Cycle();
                allocations.Start(); var collections = GC.CollectionCount(0);
                for (var i = 0; i < cycles; i++) { start = Stopwatch.GetTimestamp(); Cycle(); times[i] = Milliseconds(Stopwatch.GetTimestamp() - start); }
                allocations.Stop();
                Array.Sort(times); destroy.Dispose();
                return new Sample { pooled = pooled, allocationCounterSupported = allocations.Supported, batchSize = batch, cycles = cycles,
                    shots = cycles * batch, allocatedBytes = allocations.Bytes, allocationEvents = allocations.Events, gen0Collections = GC.CollectionCount(0) - collections,
                    preparedEntities = pool?.CreatedCount ?? 0, preparationMilliseconds = preparation,
                    medianBatchMilliseconds = times[cycles / 2], p95BatchMilliseconds = times[(int)(cycles * .95)] };
            } finally { Object.DestroyImmediate(config); }
        }
        private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    }
}

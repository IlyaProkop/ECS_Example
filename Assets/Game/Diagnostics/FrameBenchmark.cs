using System;
using System.Collections;
using System.IO;
using Game.Domain;
using Game.ECS.Core;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Diagnostics {
    // Explicitly requested only. Exercises the actual factory/run/presenter with value input;
    // no user input, saves, screenshot capture or runtime dependency on the measurement code.
    public sealed class FrameBenchmark : MonoBehaviour {
        private int sampleFrames = 600;
        private BenchmarkArena arena;
        private FrameMetrics metrics;
        private string output;
        private bool failed;
        private Report report;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunIfRequested() {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-arena-frame-benchmark") < 0) return;
            var host = new GameObject("FrameBenchmark");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<FrameBenchmark>();
            var index = Array.IndexOf(args, "-arena-frame-output");
            runner.output = index >= 0 && index + 1 < args.Length ? Path.GetFullPath(args[index + 1]) :
                Path.Combine(Application.persistentDataPath, "frame-benchmark.json");
        }
        private void OnEnable() => Application.logMessageReceived += this.OnLog;
        private void OnDisable() => Application.logMessageReceived -= this.OnLog;
        private void OnLog(string message, string stack, LogType type) {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            this.failed = true;
            Application.Quit(1);
        }
        private IEnumerator Start() {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureDeltaTime = GameSession.FixedStep;
            // Allow the normal bootstrap router to finish before replacing its menu scene.
            yield return null; yield return null;
            var previous = SceneManager.GetActiveScene();
            var isolated = SceneManager.CreateScene("FrameBenchmarkScene");
            SceneManager.SetActiveScene(isolated);
            yield return SceneManager.UnloadSceneAsync(previous);
            var args = Environment.GetCommandLineArgs();
            var countIndex = Array.IndexOf(args, "-arena-frame-counts");
            var counts = countIndex < 0 ? new[] { 12, 128, 512 } :
                Array.ConvertAll(args[countIndex + 1].Split(','), int.Parse);
            foreach (var count in counts) Require(count >= 1 && count <= 10000, "Population must be 1..10000.");
            var sampleIndex = Array.IndexOf(args, "-arena-frame-samples");
            if (sampleIndex >= 0) this.sampleFrames = int.Parse(args[sampleIndex + 1]);
            Require(this.sampleFrames >= 600 && this.sampleFrames <= 36000, "Sample count must be 600..36000.");
            this.report = new Report { unity = Application.unityVersion, cpu = SystemInfo.processorType,
                gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                development = Debug.isDebugBuild, width = Screen.width, height = Screen.height,
                sampleFrames = this.sampleFrames, cases = new Case[counts.Length] };
            var scenarioIndex = Array.IndexOf(args, "-arena-frame-scenario");
            var scenario = scenarioIndex < 0 ? "route" : args[scenarioIndex + 1];
            var labelIndex = Array.IndexOf(args, "-arena-frame-label-limit");
            var labelLimit = labelIndex < 0 ? -1 : int.Parse(args[labelIndex + 1]);
            for (var i = 0; i < counts.Length; i++) {
                var count = counts[i];
                this.arena = new BenchmarkArena(count, true, labelLimit, scenario);
                // One fixed step per rendered frame, including population warmup.
                for (var frame = 0; frame < count + 240 + this.arena.SettleFrames; frame++) { this.arena.Tick(); yield return null; }
                var actors = new ActorView[this.arena.Session.ViewCapacity];
                var actorCount = this.arena.Session.CopyActors(actors);
                var enemies = 0;
                for (var a = 0; a < actorCount; a++) if (actors[a].kind == ActorKind.Enemy) enemies++;
                Require(enemies == count, "The entire requested population must be alive before sampling.");
                var before = BenchmarkPopulation.Read(actors, actorCount, this.arena.Session.PlayerPosition);
                this.metrics = new FrameMetrics(this.sampleFrames);
                for (var frame = 0; frame < 8; frame++) { this.arena.Tick(); yield return null; }
                var baselineBytes = this.metrics.LastValue("GC Allocated In Frame");
                var probe = new byte[65536];
                this.arena.Tick(); yield return null;
                var probeDelta = this.metrics.LastValue("GC Allocated In Frame") - baselineBytes;
                GC.KeepAlive(probe); probe = null;
                for (var frame = 0; frame < 8; frame++) { this.arena.Tick(); yield return null; }
                var wallTimes = new double[this.sampleFrames];
                var collections = GC.CollectionCount(0);
                var timestamp = Time.realtimeSinceStartupAsDouble;
                for (var frame = 0; frame < this.sampleFrames; frame++) {
                    this.arena.Tick();
                    yield return null;
                    var now = Time.realtimeSinceStartupAsDouble;
                    wallTimes[frame] = (now - timestamp) * 1000;
                    timestamp = now;
                    this.metrics.Sample();
                }
                var sampleCollections = GC.CollectionCount(0) - collections;
                Require(this.arena.Session.Snapshot.phase == SessionPhase.Combat, "Combat ended during sampling.");
                Require(this.metrics.LastValue("Batches Count") > 0, "Unity skipped rendering. Run with a visible player window.");
                actorCount = this.arena.Session.CopyActors(actors);
                var finalEnemies = 0;
                for (var a = 0; a < actorCount; a++) if (actors[a].kind == ActorKind.Enemy) finalEnemies++;
                Require(finalEnemies == count, "Population changed during sampling.");
                var after = BenchmarkPopulation.Read(actors, actorCount, this.arena.Session.PlayerPosition);
                var overBudget = 0;
                foreach (var milliseconds in wallTimes) if (milliseconds > 1000d / 60d) overBudget++;
                this.report.cases[i] = new Case { scenario = scenario, warmupFrames = count + 240 + this.arena.SettleFrames, before = before, after = after, enemies = count, labelCapacity = this.arena.Scene.EnemyHealthUi.Capacity,
                    arenaHalfSize = this.arena.ArenaHalfSize, finalEnemies = finalEnemies, framesOver60HzBudget = overBudget,
                    gcCounterCalibrated = baselineBytes >= 0 && probeDelta >= 65536, gcProbeDelta = probeDelta,
                    gen0Collections = sampleCollections,
                    wallMilliseconds = Distribution.Read(wallTimes, this.sampleFrames), metrics = this.metrics.Read() };
                this.metrics.Dispose(); this.metrics = null;
                this.arena.Dispose(); this.arena = null;
                yield return null; yield return null;
                this.WriteReport();
            }
            if (Array.IndexOf(args, "-arena-frame-skip-lifetimes") >= 0) {
                this.report.completed = !this.failed;
                this.WriteReport();
                Application.Quit(this.failed ? 1 : 0);
                yield break;
            }
            // Warm caches above; compare equal workloads with three restarts per scene lifetime.
            var lifetimes = new Lifetime[6];
            for (var cycle = 0; cycle < lifetimes.Length; cycle++) {
                this.arena = new BenchmarkArena(12, false, -1);
                var sessions = new WeakReference[4];
                var active = new ResourcesSnapshot[4];
                for (var restart = 0; restart < sessions.Length; restart++) {
                    // Advance simulation directly outside frame measurements; render the result
                    // through the real run/controller so result bindings and restart execute.
                    this.arena.FinishBattle();
                    yield return null; yield return null;
                    sessions[restart] = new WeakReference(this.arena.Session);
                    active[restart] = ResourcesSnapshot.Capture();
                    if (restart > 0) Require(active[restart].SameObjects(active[0]), "Resources grew across restart.");
                    if (restart + 1 < sessions.Length) Require(this.arena.Flow.Restart(), "Restart rejected.");
                }
                Require(this.arena.Flow.ReturnToMenu(), "Return to menu rejected.");
                Require(this.arena.Session == null, "Factory retained the ended session.");
                this.arena.Dispose(); this.arena = null;
                yield return null; yield return null;
                yield return Resources.UnloadUnusedAssets();
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                var retained = 0;
                foreach (var session in sessions) if (session.IsAlive) retained++;
                lifetimes[cycle] = new Lifetime { cycle = cycle, retainedSessions = retained,
                    activeRuns = active, afterSceneDispose = ResourcesSnapshot.Capture() };
                Require(retained == 0, "Ended sessions survived collection.");
                if (cycle > 0) Require(lifetimes[cycle].afterSceneDispose.SameObjects(lifetimes[cycle - 1].afterSceneDispose),
                    "Unity objects grew across scene lifetimes.");
            }
            this.report.lifetimes = lifetimes;
            this.report.completed = !this.failed;
            this.WriteReport();
            Application.Quit(this.failed ? 1 : 0);
        }
        private void WriteReport() {
            Directory.CreateDirectory(Path.GetDirectoryName(this.output));
            File.WriteAllText(this.output, JsonUtility.ToJson(this.report, true));
        }
        private void OnDestroy() { this.metrics?.Dispose(); this.arena?.Dispose(); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

        [Serializable] private sealed class Report {
            public string unity, cpu, gpu, graphicsApi;
            public bool development, completed;
            public int width, height, sampleFrames;
            public Case[] cases;
            public Lifetime[] lifetimes;
        }
        [Serializable] private sealed class Case {
            public string scenario;
            public int warmupFrames;
            public BenchmarkPopulation before, after;
            public Vector2 arenaHalfSize;
            public int enemies, finalEnemies, framesOver60HzBudget, labelCapacity, gen0Collections;
            public bool gcCounterCalibrated;
            public long gcProbeDelta;
            public Distribution wallMilliseconds;
            public FrameMetrics.Result[] metrics;
        }
        [Serializable] private sealed class Lifetime {
            public int cycle, retainedSessions;
            public ResourcesSnapshot[] activeRuns;
            public ResourcesSnapshot afterSceneDispose;
        }
        [Serializable] private sealed class ResourcesSnapshot {
            public int gameObjects, materials, meshes, texts, canvases;
            public long managedBytes, unityAllocatedBytes;
            public bool SameObjects(ResourcesSnapshot other) => this.gameObjects == other.gameObjects &&
                this.materials == other.materials && this.meshes == other.meshes && this.texts == other.texts && this.canvases == other.canvases;
            public static ResourcesSnapshot Capture() => new ResourcesSnapshot {
                gameObjects = Resources.FindObjectsOfTypeAll<GameObject>().Length,
                materials = Resources.FindObjectsOfTypeAll<Material>().Length,
                meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length,
                texts = Resources.FindObjectsOfTypeAll<Text>().Length,
                canvases = Resources.FindObjectsOfTypeAll<Canvas>().Length,
                managedBytes = GC.GetTotalMemory(false), unityAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong()
            };
        }
    }
}

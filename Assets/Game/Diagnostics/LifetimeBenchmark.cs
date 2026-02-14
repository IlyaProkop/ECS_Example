using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Config;
using Game.Domain;
using Game.ECS.Core;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

namespace Game.Diagnostics {
    // Opt-in retention audit, separate from frame timing. Fixed storage and reused weak
    // handles; no report serialization or object enumeration between collection and sampling.
    public sealed class LifetimeBenchmark : MonoBehaviour {
        private const int RunsPerCycle = 4;
        private BenchmarkArena arena;
        private GameObject fixtureRoot;
        private GameSession simulation;
        private GameConfig config;
        private RetentionProbe probe;
        private Report report;
        private string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunIfRequested() {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-arena-lifetime-benchmark") < 0) return;
            var host = new GameObject("LifetimeBenchmark");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<LifetimeBenchmark>();
            runner.output = Path.GetFullPath(Argument(args, "-arena-lifetime-output", "lifetime-benchmark.json"));
            var mode = Argument(args, "-arena-lifetime-mode", "full");
            if (mode != "full" && mode != "simulation" && mode != "empty" && mode != "camera" && mode != "ui" && mode != "input")
                throw new ArgumentException("Unknown lifetime mode.");
            var warmup = int.Parse(Argument(args, "-arena-lifetime-warmup", "8"));
            var cycles = int.Parse(Argument(args, "-arena-lifetime-cycles", "64"));
            if (warmup < 1 || cycles < 2 || cycles > 4096 || warmup > 4096)
                throw new ArgumentOutOfRangeException(nameof(cycles));
            runner.report = new Report { mode = mode, warmupCycles = warmup, runsPerCycle = RunsPerCycle,
                samples = new Sample[warmup + cycles] };
        }

        private static string Argument(string[] args, string name, string fallback) {
            var index = Array.IndexOf(args, name);
            if (index < 0) return fallback;
            if (index + 1 == args.Length) throw new ArgumentException(name + " requires a value.");
            return args[index + 1];
        }
        private void OnEnable() => Application.logMessageReceived += this.OnLog;
        private void OnDisable() => Application.logMessageReceived -= this.OnLog;
        private void OnLog(string message, string stack, LogType type) {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Application.Quit(1);
        }

        private IEnumerator Start() {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureDeltaTime = GameSession.FixedStep;
            yield return null; yield return null;
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("LifetimeBenchmarkScene"));
            yield return SceneManager.UnloadSceneAsync(previous);
            this.config = Instantiate(Resources.Load<GameConfig>("GameConfig"));
            this.config.enemyCount = 12;
            this.config.enemySpawnInterval = GameSession.FixedStep;
            this.config.coinDropChance = 0;
            this.probe = new RetentionProbe();
            this.report.trackedRoles = RetentionProbe.Roles;
            this.report.unity = Application.unityVersion;
            this.report.development = Debug.isDebugBuild;
            this.report.cpu = SystemInfo.processorType;
            this.report.graphicsApi = SystemInfo.graphicsDeviceType.ToString();

            for (var cycle = 0; cycle < this.report.samples.Length; cycle++) {
                if (this.report.mode == "full") this.arena = new BenchmarkArena(12, false, -1);
                if (this.report.mode == "camera") this.fixtureRoot = new GameObject("GameCamera", typeof(Camera));
                if (this.report.mode == "ui" || this.report.mode == "input") {
                    this.fixtureRoot = new GameObject("IsolatedUi");
                    if (this.report.mode == "ui") Game.UI.Layout.ArenaUiBuilder.Create(this.fixtureRoot.transform);
                    else Game.UI.Layout.UiElements.EnsureEventSystem(this.fixtureRoot.transform);
                }
                for (var run = 0; run < RunsPerCycle; run++) {
                    this.ExecuteRun(run);
                    yield return null; yield return null;
                    if (this.arena != null && run + 1 < RunsPerCycle && !this.arena.Flow.Restart())
                        throw new InvalidOperationException("Restart rejected.");
                }
                this.CloseCycle();
                yield return null; yield return null;
                yield return Resources.UnloadUnusedAssets();
                // Enumeration creates temporary arrays/wrappers. Drop its call stack and
                // collect on a later frame before measuring any managed bytes.
                this.report.samples[cycle].objects = ObjectCounts.Capture();
                yield return null;
                Collect();
                yield return null;
                Collect();
                this.report.samples[cycle].managedBytes = GC.GetTotalMemory(false);
                this.report.samples[cycle].monoUsedBytes = Profiler.GetMonoUsedSizeLong();
                this.report.samples[cycle].monoReservedBytes = Profiler.GetMonoHeapSizeLong();
                this.report.samples[cycle].unityAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong();
                this.report.samples[cycle].retainedMask = this.probe.RetainedMask();
                if (this.report.samples[cycle].retainedMask != 0)
                    throw new InvalidOperationException("Disposed game objects survived collection; mask=" + this.report.samples[cycle].retainedMask);
                if (cycle > 0 && !this.report.samples[cycle].objects.Same(this.report.samples[cycle - 1].objects))
                    throw new InvalidOperationException("Native objects grew after scene disposal.");
            }
            this.report.completed = true;
            Directory.CreateDirectory(Path.GetDirectoryName(this.output));
            File.WriteAllText(this.output, JsonUtility.ToJson(this.report, true));
            Application.Quit(0);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ExecuteRun(int run) {
            if (this.arena != null) {
                this.arena.FinishBattle();
                this.probe.TrackRun(run, this.arena.Session, ReadField(this.arena.Flow, "run"));
            } else if (this.report.mode == "simulation") {
                this.simulation = new GameSession(this.config.CreateSimulationSettings(), captureRenderFrames: true);
                for (var i = 0; i < 6000 && this.simulation.Snapshot.phase != SessionPhase.Meta; i++)
                    this.simulation.TickUpdate(GameSession.FixedStep, new Game.Input.PlayerInputFrame {
                        move = this.simulation.Snapshot.phase == SessionPhase.AwaitingEntry ? Vector2.up : Vector2.zero });
                if (this.simulation.Snapshot.phase != SessionPhase.Meta) throw new InvalidOperationException("Battle did not finish.");
                this.probe.TrackRun(run, this.simulation, null);
                this.simulation.Dispose(); this.simulation = null;
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void CloseCycle() {
            this.probe.TrackInput();
            if (this.fixtureRoot != null) { Destroy(this.fixtureRoot); this.fixtureRoot = null; }
            if (this.arena == null) return;
            this.probe.TrackScene(this.arena);
            if (!this.arena.Flow.ReturnToMenu() || this.arena.Session != null)
                throw new InvalidOperationException("Return to menu retained the session.");
            this.arena.Dispose(); this.arena = null;
        }
        private static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        private void OnDestroy() { this.simulation?.Dispose(); this.arena?.Dispose(); if (this.config != null) Destroy(this.config); if (this.fixtureRoot != null) Destroy(this.fixtureRoot); }

        // Reflection stays inside Diagnostics; no extra public gameplay API for probes.
        private static object ReadField(object owner, string name) {
            if (owner == null) return null;
            var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(owner.GetType().FullName, name);
            return field.GetValue(owner) ?? throw new InvalidOperationException("Missing live probe target: " + name);
        }
        private sealed class RetentionProbe {
            public static readonly string[] Roles = { "session", "world", "context", "run", "presenter", "uiSession", "hudModel", "resultsModel", "factory", "scene", "flow", "inputModule", "inputAsset", "inputAction", "inputScope" };
            private readonly WeakReference[] targets = new WeakReference[RunsPerCycle * 8 + 7];
            public RetentionProbe() { for (var i = 0; i < this.targets.Length; i++) this.targets[i] = new WeakReference(null); }
            public void TrackRun(int run, GameSession session, object battleRun) {
                var offset = run * 8;
                this.targets[offset].Target = session;
                this.targets[offset + 1].Target = ReadField(session, "world");
                this.targets[offset + 2].Target = ReadField(session, "context");
                this.targets[offset + 3].Target = battleRun;
                this.targets[offset + 4].Target = ReadField(battleRun, "presenter");
                var ui = ReadField(battleRun, "ui");
                this.targets[offset + 5].Target = ui;
                this.targets[offset + 6].Target = ReadField(ui, "hud");
                this.targets[offset + 7].Target = ReadField(ui, "results");
            }
            public void TrackScene(BenchmarkArena arena) {
                this.targets[32].Target = ReadField(arena, "factory");
                this.targets[33].Target = arena.Scene;
                this.targets[34].Target = arena.Flow;
            }
            public void TrackInput() {
                var module = UnityEngine.Object.FindFirstObjectByType<InputSystemUIInputModule>();
                this.targets[35].Target = module;
                this.targets[36].Target = module != null ? module.actionsAsset : null;
                this.targets[37].Target = module != null ? module.point.action : null;
                this.targets[38].Target = module != null ? module.GetComponent<Game.UI.SceneUiInputScope>() : null;
            }
            public int RetainedMask() {
                var mask = 0;
                for (var i = 0; i < this.targets.Length; i++)
                    if (this.targets[i].IsAlive) mask |= 1 << (i < 32 ? i % 8 : i - 24);
                return mask;
            }
        }
        [Serializable] private sealed class Report {
            public string mode, unity, cpu, graphicsApi;
            public bool development, completed;
            public int warmupCycles, runsPerCycle;
            public string[] trackedRoles;
            public Sample[] samples;
        }
        [Serializable] private struct Sample {
            public long managedBytes, monoUsedBytes, monoReservedBytes, unityAllocatedBytes;
            public int retainedMask;
            public ObjectCounts objects;
        }
        [Serializable] private struct ObjectCounts {
            private static readonly FieldInfo ActionReferences = typeof(InputSystemUIInputModule).GetField(
                "s_InputActionReferenceCounts", BindingFlags.Static | BindingFlags.NonPublic) ??
                throw new MissingFieldException("Input System action reference registry changed; update the diagnostic probe.");
            public int gameObjects, materials, meshes, texts, canvases, inputActionReferences;
            public static ObjectCounts Capture() => new ObjectCounts {
                gameObjects = Resources.FindObjectsOfTypeAll<GameObject>().Length,
                materials = Resources.FindObjectsOfTypeAll<Material>().Length,
                meshes = Resources.FindObjectsOfTypeAll<Mesh>().Length,
                texts = Resources.FindObjectsOfTypeAll<Text>().Length,
                canvases = Resources.FindObjectsOfTypeAll<Canvas>().Length,
                inputActionReferences = ((IDictionary)ActionReferences.GetValue(null)).Count
            };
            public bool Same(ObjectCounts other) => this.gameObjects == other.gameObjects && this.materials == other.materials &&
                this.meshes == other.meshes && this.texts == other.texts && this.canvases == other.canvases &&
                this.inputActionReferences == other.inputActionReferences;
        }
    }
}

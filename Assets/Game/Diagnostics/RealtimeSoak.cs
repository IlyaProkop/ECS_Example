using System;
using System.Collections;
using System.IO;
using Game.Domain;
using Game.ECS.Core;
using Game.Flow;
using Game.Progression;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace Game.Diagnostics {
    // Actual Bootstrap/Menu/Game scene hosts, normal deltaTime and automatic GC.
    // The harness drives input/buttons; it never advances or disposes a simulation itself.
    [DefaultExecutionOrder(-10000)]
    public sealed class RealtimeSoak : MonoBehaviour {
        private enum Stage { Menu, LoadingBattle, VictoryCombat, VictoryResult, Restart,
            DefeatCombat, DefeatResult, LoadingMenu, Finished }
        private GameBootstrap bootstrap;
        private SoakInput input;
        private SoakFrameLog frames;
        private SoakProfilerCapture profilerCapture;
        private Report report;
        private string output, progressPath;
        private Stage stage, frameStage;
        private int cycle, frameCycle, memoryCount;
        private double started, lastFrame, stageStarted, battleStarted, combatStarted, combatFinished, nextMemory;
        private bool sampling, failed;
        private WeakReference[] sessions;
        private double totalDropped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RunIfRequested() {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-arena-realtime-soak") < 0) return;
            var outputIndex = Array.IndexOf(args, "-arena-soak-output");
            var cyclesIndex = Array.IndexOf(args, "-arena-soak-cycles");
            if (outputIndex < 0 || outputIndex + 1 == args.Length) throw new ArgumentException("Provide -arena-soak-output directory.");
            var cycles = cyclesIndex >= 0 && cyclesIndex + 1 < args.Length ? int.Parse(args[cyclesIndex + 1]) : 16;
            if (cycles < 1 || cycles > 120) throw new ArgumentOutOfRangeException(nameof(cycles));
            var output = Path.GetFullPath(args[outputIndex + 1]);
            Directory.CreateDirectory(output);
            var progressPath = Path.Combine(output, "progress.json");
            if (File.Exists(progressPath) || File.Exists(progressPath + ".bak")) throw new IOException("Use a new soak output directory.");
            ApplicationServices.ConfigureProgressStore(new JsonProgressStore(progressPath));
            var host = new GameObject("RealtimeSoak");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<RealtimeSoak>();
            runner.output = output; runner.progressPath = progressPath;
            runner.report = new Report { profiled = Array.IndexOf(args, "-arena-soak-profile") >= 0,
                cyclesRequested = cycles, runs = new Run[cycles * 2],
                memory = new Memory[cycles * 120 + 30], stages = Enum.GetNames(typeof(Stage)) };
            runner.sessions = new WeakReference[cycles * 2];
            for (var i = 0; i < runner.sessions.Length; i++) runner.sessions[i] = new WeakReference(null);
        }
        private void OnEnable() {
            SceneManager.sceneLoaded += this.OnSceneLoaded;
            Application.logMessageReceived += this.OnLog;
        }
        private void OnDisable() {
            SceneManager.sceneLoaded -= this.OnSceneLoaded;
            Application.logMessageReceived -= this.OnLog;
        }
        // AutoBootstrap also handles sceneLoaded and may run after this callback.
        // Resolve its host from the load stage, after all scene callbacks have completed.
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => this.bootstrap = null;
        private void OnLog(string message, string stack, LogType type) {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            this.failed = true;
            Application.Quit(1);
        }
        private IEnumerator Start() {
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            Require(Time.captureDeltaTime == 0 && Time.timeScale == 1, "Use ordinary game time.");
            Require(GarbageCollector.GCMode == GarbageCollector.Mode.Enabled, "Automatic GC must remain enabled.");
            var deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (SceneManager.GetActiveScene().name != "Menu") {
                Require(Time.realtimeSinceStartupAsDouble < deadline, "Initial menu did not load.");
                yield return null;
            }
            this.input = new SoakInput(() => this.bootstrap != null ? this.bootstrap.Session : null);
            var warmUntil = Time.realtimeSinceStartupAsDouble + 3;
            while (Time.realtimeSinceStartupAsDouble < warmUntil) yield return null;
            this.frames = new SoakFrameLog(this.report.cyclesRequested * 6000 + 1800);
            for (var i = 0; i < 8; i++) yield return null;
            var baseline = this.frames.Allocated;
            var probe = new byte[65536];
            yield return null;
            this.report.allocationProbeDelta = this.frames.Allocated - baseline;
            GC.KeepAlive(probe); probe = null;
            for (var i = 0; i < 8; i++) yield return null;
            this.report.allocationCounterCalibrated = baseline >= 0 && this.report.allocationProbeDelta >= 65536;
            this.report.collectionMarkerAvailable = this.frames.CollectionMarkerAvailable;
            this.report.incrementalMarkerAvailable = this.frames.IncrementalMarkerAvailable;
            this.report.gcMarkers = this.frames.AvailableGcMarkers;
            this.report.unity = Application.unityVersion;
            this.report.development = Debug.isDebugBuild;
            this.report.cpu = SystemInfo.processorType;
            this.report.gpu = SystemInfo.graphicsDeviceName;
            this.report.graphicsApi = SystemInfo.graphicsDeviceType.ToString();
            this.report.incrementalGc = GarbageCollector.isIncremental;
            this.report.targetFrameRate = Application.targetFrameRate;
            this.report.width = Screen.width; this.report.height = Screen.height;
            if (this.report.profiled) this.profilerCapture = new SoakProfilerCapture(Path.Combine(this.output, "cpu.raw"));
            this.started = this.lastFrame = this.stageStarted = Time.realtimeSinceStartupAsDouble;
            this.nextMemory = this.started;
            this.sampling = true;
        }

        private void Update() {
            if (!this.sampling) return;
            var now = Time.realtimeSinceStartupAsDouble;
            this.profilerCapture?.Update(now - this.started);
            this.frames.Add(now - this.started, (now - this.lastFrame) * 1000, (int)this.frameStage,
                this.frameCycle, GC.CollectionCount(0), this.totalDropped +
                ((this.stage == Stage.VictoryCombat || this.stage == Stage.DefeatCombat) && this.bootstrap != null ? this.bootstrap.Session?.DroppedSimulationSeconds ?? 0 : 0));
            this.lastFrame = now;
            if (now >= this.nextMemory) {
                Require(this.memoryCount < this.report.memory.Length, "Memory sample capacity exceeded.");
                this.report.memory[this.memoryCount++] = new Memory { seconds = now - this.started, cycle = this.cycle,
                    stage = (int)this.stage, managedBytes = Profiler.GetMonoUsedSizeLong(), reservedBytes = Profiler.GetMonoHeapSizeLong(),
                    unityAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong(), collections = GC.CollectionCount(0),
                    retiredSessionsAlive = this.RetiredSessionsAlive() };
                this.nextMemory = now + 1;
            }
        }
        private void LateUpdate() {
            if (!this.sampling || this.failed) return;
            var now = Time.realtimeSinceStartupAsDouble;
            if (now - this.stageStarted >= 60) throw new InvalidOperationException("Soak stage timed out: " + this.stage);
            if (this.stage == Stage.VictoryCombat || this.stage == Stage.DefeatCombat) {
                var phase = this.bootstrap.Session.Snapshot.phase;
                if (this.combatStarted < 0 && phase == SessionPhase.Combat) this.combatStarted = now;
                if (this.combatFinished < 0 && (phase == SessionPhase.Results || phase == SessionPhase.Meta)) this.combatFinished = now;
            }
            switch (this.stage) {
                case Stage.Menu:
                    if (now - this.stageStarted < 2) break;
                    if (this.cycle == this.report.cyclesRequested) { this.Finish(); return; }
                    this.input.BeginBattle(true);
                    this.ChangeStage(Stage.LoadingBattle, now);
                    Click("PlayButton");
                    break;
                case Stage.LoadingBattle:
                    if (this.bootstrap == null) this.bootstrap = FindFirstObjectByType<GameBootstrap>();
                    if (this.bootstrap == null || ApplicationServices.Flow.State != GameFlowState.Battle) break;
                    this.battleStarted = now;
                    this.combatStarted = this.combatFinished = -1;
                    this.ChangeStage(Stage.VictoryCombat, now);
                    break;
                case Stage.VictoryCombat:
                    if (ApplicationServices.Flow.State != GameFlowState.Results) break;
                    this.RecordResult(true, now);
                    this.ChangeStage(Stage.VictoryResult, now);
                    break;
                case Stage.VictoryResult:
                    if (now - this.stageStarted < 2) break;
                    this.input.BeginBattle(false);
                    this.ChangeStage(Stage.Restart, now);
                    Click("RestartButton");
                    break;
                case Stage.Restart:
                    Require(this.bootstrap != null && ApplicationServices.Flow.State == GameFlowState.Battle, "Restart did not create a battle.");
                    this.battleStarted = now;
                    this.combatStarted = this.combatFinished = -1;
                    this.ChangeStage(Stage.DefeatCombat, now);
                    break;
                case Stage.DefeatCombat:
                    if (ApplicationServices.Flow.State != GameFlowState.Results) break;
                    this.RecordResult(false, now);
                    this.ChangeStage(Stage.DefeatResult, now);
                    break;
                case Stage.DefeatResult:
                    if (now - this.stageStarted < 2) break;
                    this.ChangeStage(Stage.LoadingMenu, now);
                    Click("MenuButton");
                    break;
                case Stage.LoadingMenu:
                    if (SceneManager.GetActiveScene().name != "Menu") break;
                    Require(this.bootstrap == null && ApplicationServices.Flow.State == GameFlowState.Menu, "Menu retained the battle host.");
                    this.cycle++;
                    Debug.Log($"Soak cycle {this.cycle}/{this.report.cyclesRequested}; elapsed={now - this.started:F1}s; managed={Profiler.GetMonoUsedSizeLong()}; collections={GC.CollectionCount(0)}");
                    this.ChangeStage(Stage.Menu, now);
                    break;
            }
            this.frameStage = this.stage; this.frameCycle = this.cycle;
        }
        private void ChangeStage(Stage value, double now) { this.stage = value; this.stageStarted = now; }
        private void RecordResult(bool victory, double now) {
            var session = this.bootstrap.Session;
            Require(session.TryGetResult(out var result), "Results are not available.");
            Require(result.outcome == (victory ? BattleOutcome.Victory : BattleOutcome.Defeat), "Unexpected battle outcome.");
            Require(victory ? result.statistics.kills == 12 && result.statistics.abilityUses > 0 : result.statistics.attacks == 0,
                "Input script did not exercise the expected battle.");
            Require(this.combatStarted >= 0 && this.combatFinished >= this.combatStarted, "Combat boundaries were not observed.");
            var index = this.cycle * 2 + (victory ? 0 : 1);
            this.report.runs[index] = new Run { sessionId = result.sessionId, outcome = result.outcome, statistics = result.statistics,
                reward = result.reward, wallSeconds = now - this.battleStarted,
                combatWallSeconds = this.combatFinished - this.combatStarted,
                resultDelaySeconds = now - this.combatFinished,
                droppedSimulationSeconds = session.DroppedSimulationSeconds };
            this.sessions[index].Target = session;
            this.totalDropped += session.DroppedSimulationSeconds;
            var progress = ApplicationServices.Progress.Current;
            Require(progress.battles == index + 1 && progress.victories == this.cycle + 1, "Progress was lost or recorded twice.");
            Require(!ApplicationServices.Progress.HasPendingSave, "Progress was not saved.");
        }
        private int RetiredSessionsAlive() {
            var count = 0;
            for (var i = 0; i < this.sessions.Length; i++) {
                // The most recently completed run can still own its Results screen.
                if (i == this.cycle * 2 + (this.input.Attack ? 0 : 1) && this.bootstrap != null) continue;
                if (this.sessions[i].IsAlive) count++;
            }
            return count;
        }
        private static void Click(string name) {
            var target = GameObject.Find(name);
            Require(target != null, "Missing UI button: " + name);
            var button = target.GetComponent<Button>();
            Require(button != null && button.IsActive() && button.IsInteractable(), "Button is not available: " + name);
            button.onClick.Invoke();
        }
        private void Finish() {
            this.sampling = false;
            this.report.elapsedSeconds = Time.realtimeSinceStartupAsDouble - this.started;
            this.report.cyclesCompleted = this.cycle;
            this.report.frameCount = this.frames.Count;
            this.report.memoryCount = this.memoryCount;
            Array.Resize(ref this.report.memory, this.memoryCount);
            this.report.finalProgress = new JsonProgressStore(this.progressPath).Load();
            Require(this.report.finalProgress.battles == this.cycle * 2 && this.report.finalProgress.victories == this.cycle,
                "Saved progress did not survive a fresh store read.");
            Require(this.frames.Batches > 0, "Rendering was skipped; keep the player visible.");
            this.report.completed = true;
            this.frames.Write(Path.Combine(this.output, "frames.bin"));
            File.WriteAllText(Path.Combine(this.output, "report.json"), JsonUtility.ToJson(this.report, true));
            Application.Quit(0);
        }
        private void OnDestroy() { this.profilerCapture?.Dispose(); this.input?.Dispose(); this.frames?.Dispose(); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        [Serializable] private sealed class Report {
            public string unity, cpu, gpu, graphicsApi;
            public bool completed, development, incrementalGc, allocationCounterCalibrated, collectionMarkerAvailable, incrementalMarkerAvailable, profiled;
            public int cyclesRequested, cyclesCompleted, frameCount, memoryCount, targetFrameRate, width, height;
            public long allocationProbeDelta;
            public double elapsedSeconds;
            public string[] stages, gcMarkers;
            public Run[] runs;
            public Memory[] memory;
            public PlayerProgress finalProgress;
        }
        [Serializable] private struct Run {
            public string sessionId;
            public BattleOutcome outcome;
            public BattleStatistics statistics;
            public int reward;
            public double wallSeconds, combatWallSeconds, resultDelaySeconds, droppedSimulationSeconds;
        }
        [Serializable] private struct Memory {
            public double seconds;
            public int cycle, stage, collections, retiredSessionsAlive;
            public long managedBytes, reservedBytes, unityAllocatedBytes;
        }
    }
}

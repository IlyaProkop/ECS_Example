using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Domain;
using Game.Progression;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class ArenaPlaythroughTests {
        private Keyboard keyboard;
        private Mouse mouse;
        private readonly List<InputDevice> suspendedDevices = new List<InputDevice>();
        private string saveDirectory;
        private float oldCaptureDelta;
        private int captureIndex;
        private string captureDirectory;
        private bool oldBackground;
        private InputSettings.BackgroundBehavior oldInputBackground;
        private InputSettings.EditorInputBehaviorInPlayMode oldEditorInput;

        [UnitySetUp]
        public IEnumerator SetUp() {
            this.oldBackground = Application.runInBackground;
            Application.runInBackground = true;
            this.oldInputBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            this.oldEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            // Real mouse/keyboard activity must not turn the idle defeat run into an attack run.
            // Only Unity's test process is isolated; restore every previously enabled device.
            foreach (var device in InputSystem.devices) {
                if (device.enabled && (device is Keyboard || device is Mouse)) {
                    this.suspendedDevices.Add(device);
                    InputSystem.DisableDevice(device);
                }
            }
            this.keyboard = InputSystem.AddDevice<Keyboard>();
            this.mouse = InputSystem.AddDevice<Mouse>();
            this.saveDirectory = Path.Combine(Application.temporaryCachePath, "ArenaPlayTests-" + Guid.NewGuid().ToString("N"));
            ApplicationServices.SetProgressStoreForTests(new JsonProgressStore(Path.Combine(this.saveDirectory, "progress.json")));
            this.oldCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 30f;
            this.captureDirectory = Environment.GetEnvironmentVariable("ARENA_CAPTURE_DIR");
            Debug.Log($"Playthrough capture: {this.captureDirectory}; playing: {Application.isPlaying}; dt: {Time.deltaTime}");
            if (!string.IsNullOrEmpty(this.captureDirectory)) Directory.CreateDirectory(this.captureDirectory);
            SceneManager.LoadScene("Game");
            var readyDeadline = Time.realtimeSinceStartupAsDouble + 20;
            while (Time.realtimeSinceStartupAsDouble < readyDeadline) {
                var host = Object.FindFirstObjectByType<GameBootstrap>();
                if (host != null && host.Session != null) break;
                yield return null;
            }
            yield return null;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator DefaultArenaVictoryDefeatRestartAndMenu() {
            var bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.Session.Snapshot.phase, Is.EqualTo(SessionPhase.AwaitingEntry));
            for (var i = 0; i < 30; i++) { this.Capture(); yield return null; }

            // Walk a perimeter route using real Input System keyboard events and default balance.
            var route = new[] {
                new Vector3(11.5f, 0f, -9.25f), new Vector3(11.5f, 0f, 9.25f),
                new Vector3(-11.5f, 0f, 9.25f), new Vector3(-11.5f, 0f, -9.25f)
            };
            var waypoint = 0;
            var sawEffect = false;
            var sawExpiration = false;
            for (var frame = 0; frame < 3600 && bootstrap.Session.Snapshot.phase != SessionPhase.Meta; frame++) {
                var session = bootstrap.Session;
                var position = session.PlayerPosition;
                var entering = session.Snapshot.phase == SessionPhase.AwaitingEntry;
                var delta = route[waypoint] - position;
                if (delta.sqrMagnitude < 0.2f) { waypoint = (waypoint + 1) % route.Length; delta = route[waypoint] - position; }
                var move = entering ? Vector3.forward : delta.normalized;
                var state = new KeyboardState();
                if (move.x > 0.15f) state.Set(Key.D, true);
                if (move.x < -0.15f) state.Set(Key.A, true);
                if (move.z > 0.15f) state.Set(Key.W, true);
                if (move.z < -0.15f) state.Set(Key.S, true);
                state.Set(Key.J, true);
                state.Set(Key.Space, frame % 151 == 0);
                InputSystem.QueueStateEvent(this.keyboard, state);
                this.Capture();
                yield return null;
                var stats = bootstrap.Session.Snapshot.playerStats;
                if (stats.ActiveEffects > 0) {
                    if (!sawEffect) Debug.Log($"Playthrough marker: empowerment frame={this.captureIndex}");
                    sawEffect = true;
                    Assert.That(stats.MoveSpeed, Is.EqualTo(8.1f).Within(0.0001f));
                    Assert.That(stats.Armor, Is.EqualTo(40f));
                    Assert.That(GameObject.Find("StatusEffects").GetComponent<Text>().text, Does.Contain("БРОНЯ 40"));
                } else if (sawEffect && bootstrap.Session.Snapshot.phase == SessionPhase.Combat) {
                    sawExpiration = true;
                    Assert.That(stats.MoveSpeed, Is.EqualTo(6f));
                    Assert.That(GameObject.Find("StatusEffects"), Is.Null);
                }
            }
            Assert.That(bootstrap.Session.TryGetResult(out var victory), Is.True,
                $"Default arena timed out: phase={bootstrap.Session.Snapshot.phase}, position={bootstrap.Session.PlayerPosition}, time={bootstrap.Session.Snapshot.statistics.duration}, kills={bootstrap.Session.Snapshot.statistics.kills}, dt={Time.deltaTime}, frame={Time.frameCount}");
            Assert.That(victory.outcome, Is.EqualTo(BattleOutcome.Victory));
            Assert.That(victory.statistics.kills, Is.EqualTo(12));
            Assert.That(sawEffect && sawExpiration, Is.True, "The default authored ability must show and expire its status in the HUD.");
            Assert.That(bootstrap.Session.Snapshot.playerStats.ActiveEffects, Is.Zero);
            Debug.Log($"Playthrough marker: victory frame={this.captureIndex}; time={victory.statistics.duration}; damage={victory.statistics.damageDealt}; received={victory.statistics.damageReceived}; reward={victory.reward}");
            Assert.That(GameObject.Find("ResultPanel"), Is.Not.Null);
            Assert.That(ApplicationServices.Progress.Current.victories, Is.EqualTo(1));
            var currency = ApplicationServices.Progress.Current.currency;
            InputSystem.QueueStateEvent(this.keyboard, new KeyboardState());
            for (var i = 0; i < 90; i++) { this.Capture(); yield return null; }

            GameObject.Find("RestartButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(bootstrap.Session.Snapshot.phase, Is.EqualTo(SessionPhase.AwaitingEntry));
            Assert.That(bootstrap.Session.Snapshot.statistics.kills, Is.Zero);
            Assert.That(bootstrap.Session.Snapshot.playerStats.ActiveEffects, Is.Zero);
            Assert.That(bootstrap.Session.Snapshot.playerStats.Armor, Is.EqualTo(10f));
            for (var frame = 0; frame < 1800 && bootstrap.Session.Snapshot.phase != SessionPhase.Meta; frame++) {
                InputSystem.QueueStateEvent(this.keyboard, bootstrap.Session.Snapshot.phase == SessionPhase.AwaitingEntry
                    ? new KeyboardState(Key.W) : new KeyboardState());
                this.Capture();
                yield return null;
            }
            Assert.That(bootstrap.Session.TryGetResult(out var defeat), Is.True);
            Assert.That(defeat.statistics.attacks, Is.Zero, "Idle run must not receive attack input from a physical device.");
            Assert.That(defeat.outcome, Is.EqualTo(BattleOutcome.Defeat));
            Assert.That(defeat.reward, Is.Zero);
            Debug.Log($"Playthrough marker: defeat frame={this.captureIndex}; time={defeat.statistics.duration}; received={defeat.statistics.damageReceived}");
            Assert.That(ApplicationServices.Progress.Current.currency, Is.EqualTo(currency));
            for (var i = 0; i < 60; i++) { this.Capture(); yield return null; }
            GameObject.Find("MenuButton").GetComponent<Button>().onClick.Invoke();
            var menuDeadline = Time.realtimeSinceStartupAsDouble + 20;
            while (SceneManager.GetActiveScene().name != "Menu" && Time.realtimeSinceStartupAsDouble < menuDeadline) {
                this.Capture(); yield return null;
            }
            for (var i = 0; i < 10; i++) { this.Capture(); yield return null; }
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"));
            Debug.Log($"Playthrough marker: menu frame={this.captureIndex}");
            Assert.That(GameObject.Find("Progress").GetComponent<Text>().text, Does.Contain(currency.ToString()));
            for (var i = 0; i < 60; i++) { this.Capture(); yield return null; }
        }

        private void Capture() {
            if (string.IsNullOrEmpty(this.captureDirectory)) return;
            ScreenCapture.CaptureScreenshot(Path.Combine(this.captureDirectory, $"frame-{this.captureIndex++:00000}.png"));
        }

        [UnityTearDown]
        public IEnumerator TearDown() {
            Time.captureDeltaTime = this.oldCaptureDelta;
            Application.runInBackground = this.oldBackground;
            InputSystem.settings.backgroundBehavior = this.oldInputBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = this.oldEditorInput;
            if (this.keyboard != null) InputSystem.RemoveDevice(this.keyboard);
            if (this.mouse != null) InputSystem.RemoveDevice(this.mouse);
            foreach (var device in this.suspendedDevices) if (device.added) InputSystem.EnableDevice(device);
            this.suspendedDevices.Clear();
            SceneManager.LoadScene("Menu");
            yield return null;
            if (!string.IsNullOrEmpty(this.saveDirectory) && Directory.Exists(this.saveDirectory)) Directory.Delete(this.saveDirectory, true);
        }
    }
}

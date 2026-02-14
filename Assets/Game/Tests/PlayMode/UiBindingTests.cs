using System.Collections;
using Game.Domain;
using Game.Flow;
using Game.UI;
using Game.UiModel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests {
    public sealed class UiBindingTests {
        private GameObject root;
        [UnityTearDown] public IEnumerator TearDown() {
            if (this.root != null) Object.Destroy(this.root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HudDisconnectsWhileDisabledAndRebindsToLatestState() {
            this.root = new GameObject("BindingTest");
            this.root.SetActive(false);
            var health = this.Text("health");
            var effects = this.Text("effects");
            var view = this.root.AddComponent<HudView>();
            view.Initialize(health, this.Text("coins"), this.Text("objective"), this.Text("controls"), effects);
            using var first = new HudViewModel("Импульс");
            using var second = new HudViewModel("Рывок");
            first.Update(Snapshot(100));
            view.Bind(first);
            Assert.That(health.text, Is.Empty);
            this.root.SetActive(true);
            yield return null;
            Assert.That(health.text, Does.Contain("100"));
            Assert.That(effects.gameObject.activeSelf, Is.False);
            first.Update(new BattleSnapshot(100, 0, 1, SessionPhase.Combat, BattleOutcome.None, default, 0f, Vector3.zero,
                new Game.Domain.Stats.PlayerStatsSnapshot(1, 3f, 8.1f, 40f)));
            Assert.That(effects.gameObject.activeSelf, Is.True);
            Assert.That(effects.text, Does.Contain("БРОНЯ 40"));
            this.root.SetActive(false);
            first.Update(Snapshot(25));
            Assert.That(health.text, Does.Contain("100"));
            this.root.SetActive(true);
            yield return null;
            Assert.That(health.text, Does.Contain("25"));
            Assert.That(effects.gameObject.activeSelf, Is.False);
            second.Update(Snapshot(80));
            view.Bind(second);
            first.Update(Snapshot(7));
            view.Unbind(first); // Delayed teardown from a previous owner.
            second.Update(Snapshot(61));
            Assert.That(health.text, Does.Contain("61"));
            view.Unbind();
            second.Update(Snapshot(90));
            Assert.That(health.text, Does.Contain("61"));
        }

        [UnityTest]
        public IEnumerator ResultViewUpdatesHiddenPanelAndOwnsExactlyOneButtonSubscription() {
            this.root = new GameObject("ResultBindingTest");
            var panel = new GameObject("panel"); panel.transform.SetParent(this.root.transform);
            var restart = this.Button("restart");
            var menu = this.Button("menu");
            var view = this.root.AddComponent<ResultsView>();
            view.Initialize(panel, this.Text("title"), this.Text("details"), restart, menu);
            var a = new Commands(); var b = new Commands();
            using var first = new ResultsViewModel(a);
            using var second = new ResultsViewModel(b);
            view.Bind(first);
            Assert.That(panel.activeSelf, Is.False);
            first.ShowResult(Result(), new PlayerProgress(), "");
            Assert.That(panel.activeSelf, Is.True);
            restart.onClick.Invoke();
            Assert.That(a.Restarts, Is.EqualTo(1));
            for (var i = 0; i < 3; i++) {
                view.enabled = false;
                restart.onClick.Invoke();
                Assert.That(a.Restarts, Is.EqualTo(1));
                view.enabled = true;
            }
            restart.onClick.Invoke();
            Assert.That(a.Restarts, Is.EqualTo(2), "Repeated enable must not duplicate listeners.");
            view.Bind(second);
            first.ShowResult(Result(), new PlayerProgress(), "stale");
            Assert.That(panel.activeSelf, Is.False);
            second.ShowResult(Result(), new PlayerProgress(), "new");
            view.Unbind(first);
            restart.onClick.Invoke();
            Assert.That(a.Restarts, Is.EqualTo(2));
            Assert.That(b.Restarts, Is.EqualTo(1));
            Object.Destroy(view);
            yield return null;
            restart.onClick.Invoke();
            Assert.That(b.Restarts, Is.EqualTo(1));
            Assert.That(restart.interactable, Is.False);
            second.ShowResult(Result(), new PlayerProgress(), "after destroy");
        }

        private Text Text(string name) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(this.root.transform);
            return go.GetComponent<Text>();
        }
        private Button Button(string name) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(this.root.transform);
            return go.GetComponent<Button>();
        }
        private static BattleSnapshot Snapshot(int health) => new BattleSnapshot(health, 0, 1,
            SessionPhase.Combat, BattleOutcome.None, default, 0f, Vector3.zero);
        private static BattleResult Result() => new BattleResult("binding-result", BattleOutcome.Victory, default, 100);
        private sealed class Commands : IGameCommands {
            public int Restarts;
            public bool Play() => false;
            public bool Restart() { this.Restarts++; return false; }
            public bool ReturnToMenu() => false;
        }
    }
}

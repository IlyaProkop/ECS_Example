using System.Collections;
using Game.Domain.Abilities;
using Game.UI;
using Game.UiModel;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class AbilityBindingTests {
        private GameObject root;
        [UnityTearDown] public IEnumerator TearDown() { if (this.root != null) Object.Destroy(this.root); yield return null; }
        private sealed class Commands : IAbilityCommands {
            public int Calls, Slot = -1;
            public bool TryActivateAbility(int slot) { this.Calls++; this.Slot = slot; return true; }
        }
        [UnityTest]
        public IEnumerator ProductionLayoutDisplaysEveryConfiguredSlot() {
            this.root = new GameObject("Ability layout");
            var views = Game.UI.Layout.ArenaUiBuilder.Create(this.root.transform);
            views.Hud.enabled = false;
            views.Results.gameObject.transform.Find("ResultPanel").gameObject.SetActive(false);
            using var bar = new AbilityBarViewModel(new[] { "Shockwave", "Fireball", "Shield", "Dash" }, new Commands());
            var ready = new AbilitySlotSnapshot(1, 3f, 0f, true);
            bar.Update(new AbilityBarSnapshot(4, ready, new AbilitySlotSnapshot(2, 5f, 2.5f, false), ready, ready));
            views.Hud.Abilities.Bind(bar);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(views.Hud.Abilities.GetComponentsInChildren<AbilitySlotView>().Length, Is.EqualTo(4));
            var buttons = views.Hud.Abilities.GetComponentsInChildren<Button>();
            Assert.That(buttons[0].interactable, Is.True);
            Assert.That(buttons[1].interactable, Is.False);
            Assert.That(buttons[1].GetComponentInChildren<Text>().text, Does.Contain("Fireball"));
            var previousRight = float.NegativeInfinity;
            foreach (var button in buttons) {
                var corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
                Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(previousRight));
                previousRight = corners[2].x;
            }
            var capture = System.Environment.GetEnvironmentVariable("ARENA_UI_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(capture)) {
                System.IO.Directory.CreateDirectory(capture);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(capture, "ability-bar.png"));
                yield return null; yield return null;
            }
        }

        [UnityTest]
        public IEnumerator BarDisableRebindAndOldOwnerTeardownDoNotRetainCommands() {
            this.root = new GameObject("Ability bindings");
            this.root.SetActive(false);
            var views = new AbilitySlotView[4];
            var buttons = new Button[4];
            for (var i = 0; i < 4; i++) {
                var go = new GameObject("slot", typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(this.root.transform);
                buttons[i] = go.GetComponent<Button>();
                var label = new GameObject("label", typeof(RectTransform), typeof(Text)); label.transform.SetParent(go.transform);
                var progress = new GameObject("progress", typeof(RectTransform), typeof(Image)); progress.transform.SetParent(go.transform);
                views[i] = go.AddComponent<AbilitySlotView>();
                views[i].Initialize(buttons[i], label.GetComponent<Text>(), progress.GetComponent<Image>());
            }
            var view = this.root.AddComponent<AbilityBarView>(); view.Initialize(views);
            var a = new Commands(); var b = new Commands();
            using var first = new AbilityBarViewModel(new[] { "A", "B", "C", "D" }, a);
            using var second = new AbilityBarViewModel(new[] { "New" }, b);
            var ready = new AbilitySlotSnapshot(1, 2f, 0f, true);
            first.Update(new AbilityBarSnapshot(4, ready, ready, ready, ready));
            second.Update(new AbilityBarSnapshot(1, ready));
            view.Bind(first); this.root.SetActive(true); yield return null;
            buttons[3].onClick.Invoke(); Assert.That(a.Slot, Is.EqualTo(3));
            for (var i = 0; i < 3; i++) {
                view.enabled = false; buttons[0].onClick.Invoke(); Assert.That(a.Calls, Is.EqualTo(1));
                view.enabled = true;
            }
            buttons[0].onClick.Invoke(); Assert.That(a.Calls, Is.EqualTo(2));
            view.Bind(second);
            view.Unbind(first); first.Dispose();
            Assert.That(views[3].gameObject.activeSelf, Is.False);
            buttons[0].onClick.Invoke(); Assert.That(b.Calls, Is.EqualTo(1));
            buttons[3].onClick.Invoke(); Assert.That(a.Calls, Is.EqualTo(2));
            second.Update(new AbilityBarSnapshot(1, new AbilitySlotSnapshot(1, 2f, 1f, false)));
            Assert.That(buttons[0].interactable, Is.False);
            buttons[0].onClick.Invoke(); Assert.That(b.Calls, Is.EqualTo(1));
            Object.Destroy(view); yield return null;
            second.Update(new AbilityBarSnapshot(1, ready));
            buttons[0].onClick.Invoke(); Assert.That(b.Calls, Is.EqualTo(1));
        }
    }
}

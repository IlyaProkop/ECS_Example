using System.Collections;
using System.Reflection;
using Game.UI.Layout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class UiInputLifetimeTests {
        private GameObject root;
        private EventSystem[] previous;
        private static IDictionary References => (IDictionary)typeof(InputSystemUIInputModule)
            .GetField("s_InputActionReferenceCounts", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        [UnitySetUp] public IEnumerator SetUp() {
            this.previous = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            foreach (var system in this.previous) system.gameObject.SetActive(false);
            this.root = new GameObject("UiInputTest");
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown() {
            if (this.root != null) Object.Destroy(this.root);
            yield return null; yield return null;
            foreach (var system in this.previous) if (system != null) system.gameObject.SetActive(true);
        }

        [UnityTest]
        public IEnumerator SceneInputReleasesActionsAcrossDisableEnableAndDestruction() {
            var baseline = References.Count;
            for (var cycle = 0; cycle < 3; cycle++) {
                UiElements.EnsureEventSystem(this.root.transform);
                var module = this.root.GetComponentInChildren<InputSystemUIInputModule>();
                Assert.That(module, Is.Not.Null);
                var point = module.point.action;
                Assert.That(point.enabled, Is.True);
                var state = References[point];
                Assert.That((int)state.GetType().GetField("refCount").GetValue(state), Is.EqualTo(1),
                    "One live module must own one reference, including its first OnEnable.");
                for (var toggle = 0; toggle < 3; toggle++) {
                    module.enabled = false;
                    Assert.That(References.Contains(point), Is.False, "Disabled module retained its action.");
                    module.enabled = true;
                    Assert.That(module.point.action, Is.SameAs(point), "Re-enable must reuse the scene's actions.");
                    Assert.That(point.enabled, Is.True);
                }
                UiElements.EnsureEventSystem(this.root.transform);
                Assert.That(this.root.GetComponentsInChildren<EventSystem>().Length, Is.EqualTo(1));
                Assert.That(this.root.GetComponentsInChildren<InputSystemUIInputModule>().Length, Is.EqualTo(1));
                Object.Destroy(module.gameObject);
                yield return null; yield return null;
                Assert.That(References.Count, Is.EqualTo(baseline), "Destroyed scene left InputAction roots in Input System.");
            }
        }

        [UnityTest]
        public IEnumerator ExistingInputModuleRemainsOwnedByItsScene() {
            UiElements.EnsureEventSystem(this.root.transform);
            var module = this.root.GetComponentInChildren<InputSystemUIInputModule>();
            var asset = module.actionsAsset;
            var consumer = new GameObject("AnotherUi");
            try {
                UiElements.EnsureEventSystem(consumer.transform);
                Object.Destroy(consumer);
                yield return null;
                Assert.That(module != null && module.isActiveAndEnabled, Is.True);
                Assert.That(module.actionsAsset, Is.SameAs(asset));
                Assert.That(module.point.action.enabled, Is.True);
            } finally { if (consumer != null) Object.Destroy(consumer); }
        }
    }
}

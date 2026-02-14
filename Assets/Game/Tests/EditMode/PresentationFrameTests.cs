using System;
using System.Linq;
using Game.Config;
using Game.Domain;
using Game.ECS.Core;
using Game.Input;
using Game.Rendering;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class PresentationFrameTests {
        private static EnemyHealthLabel Label(int id, float hp = 50f) => new EnemyHealthLabel(id, new Vector3(100f, 100f, 1f), hp);
        private static ActorView Actor(int id, float x, float hp = 50f) => new ActorView(id, ActorKind.Enemy, Vector3.right * x, 0.5f, hp);

        [Test]
        public void LabelSlotsRetainIdentityAndReuseReleasedSlotsWithinTheSameFrame() {
            var slots = new HealthLabelSlots(3);
            slots.Bind(new[] { Label(1), Label(2), Label(3) });
            slots.Bind(new[] { Label(3), Label(4), Label(2) });
            Assert.That(slots.ItemAt(0), Is.EqualTo(1), "New actor takes the released first slot.");
            Assert.That(slots.ItemAt(1), Is.EqualTo(2), "Actor 2 retains slot 1 despite input reordering.");
            Assert.That(slots.ItemAt(2), Is.EqualTo(0), "Actor 3 retains slot 2.");
            for (var frame = 0; frame < 50; frame++) {
                slots.Bind(new[] { Label(100 + frame * 3), Label(101 + frame * 3), Label(102 + frame * 3) });
                Assert.That(new[] { slots.ItemAt(0), slots.ItemAt(1), slots.ItemAt(2) }, Is.EquivalentTo(new[] { 0, 1, 2 }));
            }
            slots.Bind(ReadOnlySpan<EnemyHealthLabel>.Empty);
            Assert.That(new[] { slots.ItemAt(0), slots.ItemAt(1), slots.ItemAt(2) }, Is.All.EqualTo(-1));
        }

        [Test]
        public void InvalidLabelFrameCannotReplacePublishedAssignments() {
            var slots = new HealthLabelSlots(2);
            slots.Bind(new[] { Label(1), Label(2) });
            Assert.Throws<ArgumentException>(() => slots.Bind(new[] { Label(3), Label(4), Label(5) }));
            Assert.Throws<ArgumentException>(() => slots.Bind(new[] { Label(3), Label(3) }));
            Assert.Throws<ArgumentException>(() => slots.Bind(new[] { Label(0) }));
            Assert.That(slots.ItemAt(0), Is.Zero);
            Assert.That(slots.ItemAt(1), Is.EqualTo(1));
            slots.Bind(new[] { Label(2) });
            Assert.That(slots.ItemAt(0), Is.EqualTo(-1));
            Assert.That(slots.ItemAt(1), Is.Zero);
        }

        [Test]
        public void HpViewNeverCreatesExtraObjectsOnFullTurnoverAndReusesThemAcrossRestart() {
            var root = new GameObject("HP view test", typeof(RectTransform), typeof(Canvas));
            try {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var view = root.AddComponent<EnemyHealthUiPresenter>();
                view.Initialize(root.GetComponent<RectTransform>());
                view.Prewarm(3);
                var ids = root.GetComponentsInChildren<Text>(true).Select(text => text.GetInstanceID()).ToArray();
                for (var frame = 0; frame < 30; frame++) {
                    view.Apply(new[] { Label(frame * 3 + 1, frame), Label(frame * 3 + 2), Label(frame * 3 + 3) });
                    Assert.That(root.GetComponentsInChildren<Text>(true).Select(text => text.GetInstanceID()), Is.EquivalentTo(ids));
                }
                view.ClearAll();
                Assert.That(root.GetComponentsInChildren<Text>(true).All(text => !text.gameObject.activeSelf), Is.True);
                view.Prewarm(3);
                view.Apply(new[] { Label(999, 7f) });
                Assert.That(root.GetComponentsInChildren<Text>(true).Select(text => text.GetInstanceID()), Is.EquivalentTo(ids));
                var visible = root.GetComponentsInChildren<Text>();
                Assert.That(visible.Length, Is.EqualTo(1));
                Assert.That(visible[0].text, Is.EqualTo("7"));
                view.Prewarm(1);
                Assert.That(root.GetComponentsInChildren<Text>(true).Length, Is.EqualTo(1));
                Assert.Throws<ArgumentException>(() => view.Apply(new[] { Label(1), Label(2) }));
                view.Prewarm(0);
                view.Apply(ReadOnlySpan<EnemyHealthLabel>.Empty);
                Assert.That(root.GetComponentsInChildren<Text>(true), Is.Empty);
            } finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FramePreparationCullsActorsCapsLabelsAndClearsThePreviousFrame() {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var root = new GameObject("Frame camera", typeof(Camera));
            try {
                var camera = root.GetComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 10f;
                camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
                root.transform.position = Vector3.up * 20f;
                root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var builder = new ActorFrameBuilder(config, 4, 2);
                var actors = new[] { Actor(1, -1f), Actor(2, 1f), Actor(3, 3f), Actor(4, 1000f) };
                var snapshot = new BattleSnapshot(100, 0, 4, SessionPhase.Combat, BattleOutcome.None, default, 0f, Vector3.zero);
                builder.Build(actors, snapshot, camera, new Vector2(800f, 600f));
                Assert.That(builder.Instances.Length, Is.EqualTo(3));
                Assert.That(builder.Labels.Length, Is.EqualTo(2));
                Assert.That(builder.Labels[0].ActorId, Is.EqualTo(1));
                Assert.That(builder.Labels[1].ActorId, Is.EqualTo(2));
                var terminal = new BattleSnapshot(0, 0, 4, SessionPhase.Results, BattleOutcome.Defeat, default, 0f, Vector3.zero);
                builder.Build(actors, terminal, camera, new Vector2(800f, 600f));
                Assert.That(builder.Instances.Length, Is.EqualTo(3));
                Assert.That(builder.Labels.Length, Is.Zero);
                builder.Build(ReadOnlySpan<ActorView>.Empty, snapshot, camera, new Vector2(800f, 600f));
                Assert.That(builder.Instances.Length, Is.Zero);
                Assert.That(builder.Labels.Length, Is.Zero);
                Assert.That(typeof(ArenaPresenter).GetMethods().SelectMany(method => method.GetParameters())
                    .Any(parameter => parameter.ParameterType == typeof(GameSession)), Is.False);
            } finally { Object.DestroyImmediate(root); Object.DestroyImmediate(config); }
        }

        [Test]
        public void InterpolationMatchesIdentityAndCurrentMembershipInsteadOfArrayOrder() {
            var history = new ActorInterpolationBuffer(3);
            var output = new ActorView[3];
            history.Capture(new[] { Actor(1, 0f), Actor(2, 10f) }, false);
            history.Capture(new[] { Actor(2, 20f, 7f), Actor(3, 30f) }, false);
            Assert.That(history.CopyTo(output, 0.5f), Is.EqualTo(2));
            Assert.That(output[0].id, Is.EqualTo(2));
            Assert.That(output[0].position.x, Is.EqualTo(15f));
            Assert.That(output[0].health, Is.EqualTo(7f));
            Assert.That(output[1].position.x, Is.EqualTo(30f), "New actor snaps to its spawn point.");
            history.Capture(new[] { Actor(1, 100f) }, false);
            Assert.That(history.CopyTo(output, 0f), Is.EqualTo(1));
            Assert.That(output[0].position.x, Is.EqualTo(100f), "An absent ID cannot inherit older history.");
            history.Capture(new[] { Actor(1, -50f) }, true);
            history.CopyTo(output, 0.5f);
            Assert.That(output[0].position.x, Is.EqualTo(-50f), "Reset snaps across a phase transition.");
        }

        [Test]
        public void RenderHistoryUsesTheLastTwoFixedStepsAndDoesNotAdvanceOnRepeatedReads() {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try {
                var settings = config.CreateSimulationSettings();
                using var session = new GameSession(settings, captureRenderFrames: true);
                using var reference = new GameSession(settings);
                var input = new PlayerInputFrame { move = Vector2.up };
                reference.TickUpdate(GameSession.FixedStep, input);
                var previous = reference.PlayerPosition;
                reference.TickUpdate(GameSession.FixedStep, input);
                var current = reference.PlayerPosition;
                session.TickUpdate(GameSession.FixedStep * 2.5f, input);
                var actors = new ActorView[session.ViewCapacity];
                session.CopyRenderActors(actors);
                Assert.That(Vector3.Distance(actors[0].position, Vector3.Lerp(previous, current, 0.5f)), Is.LessThan(0.00001f));
                var firstRead = actors[0].position;
                session.CopyRenderActors(actors);
                Assert.That(actors[0].position, Is.EqualTo(firstRead));
                session.CopyActors(actors);
                Assert.That(actors[0].position, Is.EqualTo(current));
                session.TickUpdate(GameSession.FixedStep * 0.25f, input);
                session.CopyRenderActors(actors);
                Assert.That(Vector3.Distance(actors[0].position, Vector3.Lerp(previous, current, 0.75f)), Is.LessThan(0.00001f));
                reference.CopyRenderActors(actors);
                Assert.That(actors[0].position, Is.EqualTo(current), "History is optional for headless sessions.");
            } finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void TransitionIntoCombatSnapsAllDisplayedActorsToAuthoritativePositions() {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try {
                using var session = new GameSession(config.CreateSimulationSettings(), captureRenderFrames: true);
                for (var i = 0; i < 180 && session.Snapshot.phase == SessionPhase.AwaitingEntry; i++)
                    session.TickUpdate(GameSession.FixedStep, new PlayerInputFrame { move = Vector2.up });
                Assert.That(session.Snapshot.phase, Is.EqualTo(SessionPhase.Combat));
                var rendered = new ActorView[session.ViewCapacity];
                var actual = new ActorView[session.ViewCapacity];
                var count = session.CopyActors(actual);
                Assert.That(session.CopyRenderActors(rendered), Is.EqualTo(count));
                for (var i = 0; i < count; i++) Assert.That(rendered[i].position, Is.EqualTo(actual[i].position));
            } finally { Object.DestroyImmediate(config); }
        }
    }
}

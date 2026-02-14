using System;
using Game.Config;
using Game.Domain;
using Game.ECS.Core;
using Game.Input;
using Game.Rendering;
using Game.UI;
using Game.UI.Layout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Game.Tests {
    public sealed class ActorVisualTests {
        [Test]
        public void ExtraEnemyVisualUsesItsOwnBatchGeometryAndSnapshotWithoutChangingActorKind() {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var mesh = new Mesh { bounds = new Bounds(new Vector3(1, 0.5f, 0), new Vector3(2, 3, 2)) };
            var cameraObject = new GameObject("Visual camera", typeof(Camera));
            try {
                Array.Resize(ref config.actorVisuals, 5);
                var extra = config.actorVisuals[4] = new ActorVisualAuthoring {
                    id = 77, mesh = mesh, height = 3f, meshScale = Vector3.one, elevation = 4f, color = Color.blue
                };
                var catalog = new ActorVisualCatalog(config);
                Assert.That(catalog.Count, Is.EqualTo(5));
                extra.height = 99; extra.color = Color.red; extra.id = 88;
                var actor = new ActorView(9, ActorKind.Enemy, Vector3.zero, 0.5f, 50, 77);
                var bounds = catalog[catalog.SlotFor(77)].Geometry(actor, out var position, out var scale);
                Assert.That(position, Is.EqualTo(new Vector3(0, 4, 0)));
                Assert.That(scale, Is.EqualTo(new Vector3(1, 3, 1)));
                Assert.That(bounds.center, Is.EqualTo(new Vector3(1, 5.5f, 0)));
                Assert.That(bounds.size, Is.EqualTo(new Vector3(2, 9, 2)));
                Assert.That(catalog[catalog.SlotFor(77)].Color, Is.EqualTo(Color.blue));
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true; camera.orthographicSize = 20;
                camera.transform.position = Vector3.up * 30;
                camera.transform.rotation = Quaternion.Euler(90, 0, 0);
                var builder = new ActorFrameBuilder(config, 2, 2, catalog);
                var actors = new[] { actor, new ActorView(10, ActorKind.Enemy, Vector3.right * 3, 0.5f, 50) };
                builder.Build(actors, new BattleSnapshot(100, 0, 2, SessionPhase.Combat, BattleOutcome.None, default, 0, Vector3.zero),
                    camera, new Vector2(Screen.width, Screen.height));
                Assert.That(builder.Instances.Length, Is.EqualTo(2));
                Assert.That(builder.Instances[0].Batch, Is.EqualTo(catalog.SlotFor(77)));
                Assert.That(builder.Instances[1].Batch, Is.EqualTo(catalog.SlotFor(ActorVisualIds.Enemy)));
                Assert.That(builder.Instances[0].Matrix, Is.EqualTo(Matrix4x4.TRS(position, Quaternion.identity, scale)));
                Assert.Throws<ArgumentException>(() => catalog.SlotFor(88));
            } finally { Object.DestroyImmediate(config); Object.DestroyImmediate(mesh); Object.DestroyImmediate(cameraObject); }
        }

        [TestCase("duplicate")]
        [TestCase("missing")]
        [TestCase("zero")]
        [TestCase("scale")]
        [TestCase("height")]
        [TestCase("null")]
        public void InvalidCatalogFailsAtComposition(string error) {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try {
                switch (error) {
                    case "duplicate": config.actorVisuals[1].id = config.actorVisuals[0].id; break;
                    case "missing": config.enemyTypes[0].visualId = 999; break;
                    case "zero": config.actorVisuals[0].id = 0; break;
                    case "scale": config.actorVisuals[0].meshScale = Vector3.zero; break;
                    case "height": config.actorVisuals[0].height = float.NaN; break;
                    case "null": config.actorVisuals = null; break;
                }
                Assert.Throws<ArgumentException>(() => new ActorVisualCatalog(config));
            } finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void RendererOwnsMaterialInstancesAndBorrowsAuthoredAssets() {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var mesh = new Mesh();
            var template = new Material(Resources.Load<Material>("ArenaMaterial")) { enableInstancing = false };
            try {
                config.actorVisuals[1].mesh = mesh;
                config.actorVisuals[1].materialTemplate = template;
                var catalog = new ActorVisualCatalog(config);
                var resources = InstancedRenderHelper.CreateResources(catalog[catalog.SlotFor(ActorVisualIds.Enemy)]);
                var owned = resources.material;
                try {
                    Assert.That(resources.mesh, Is.SameAs(mesh));
                    Assert.That(owned, Is.Not.SameAs(template));
                    Assert.That(owned.enableInstancing, Is.True);
                    Assert.That(template.enableInstancing, Is.False);
                } finally { InstancedRenderHelper.Dispose(ref resources); }
                Assert.That(owned == null, Is.True);
                Assert.That(template != null && mesh != null, Is.True);
                InstancedRenderHelper.Dispose(ref resources);
            } finally { Object.DestroyImmediate(config); Object.DestroyImmediate(mesh); Object.DestroyImmediate(template); }
        }

        [Test]
        public void EnemyVisualIdSurvivesAuthoritativeAndInterpolatedSessionSnapshots() {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try {
                config.enemyTypes[0].visualId = 77;
                using var session = new GameSession(config.CreateSimulationSettings(), captureRenderFrames: true);
                config.enemyTypes[0].visualId = 99;
                for (var tick = 0; tick < 120; tick++) session.TickUpdate(GameSession.FixedStep, new PlayerInputFrame { move = Vector2.up });
                var actors = new ActorView[session.ViewCapacity];
                var count = session.CopyActors(actors);
                var found = false;
                for (var i = 0; i < count; i++) if (actors[i].kind == ActorKind.Enemy) {
                    Assert.That(actors[i].visualId, Is.EqualTo(77)); found = true;
                }
                Assert.That(found, Is.True);
                session.TickUpdate(GameSession.FixedStep * 0.5f, default);
                count = session.CopyRenderActors(actors);
                for (var i = 0; i < count; i++) if (actors[i].kind == ActorKind.Enemy)
                    Assert.That(actors[i].visualId, Is.EqualTo(77));
            } finally { Object.DestroyImmediate(config); }
        }

        [TestCase(800, 600)]
        [TestCase(1920, 1080)]
        [TestCase(3840, 2160)]
        public void BatchedOverlayProjectionMatchesUnityForScaledRotatedParents(int width, int height) {
            var parent = new GameObject("Projection parent", typeof(RectTransform));
            var child = new GameObject("Projection root", typeof(RectTransform));
            try {
                child.transform.SetParent(parent.transform, false);
                parent.transform.position = new Vector3(124.25f, -45.5f, 5f);
                parent.transform.rotation = Quaternion.Euler(0f, 0f, 31f);
                parent.transform.localScale = new Vector3(0.7f, 1.3f, 1f);
                child.transform.localPosition = new Vector3(33f, 76f, 1f);
                var rect = child.GetComponent<RectTransform>();
                Assert.That(OverlayProjection.TryCreate(rect, new Vector2(width, height), out var projection), Is.True);
                for (var i = 0; i < 100; i++) {
                    var point = new Vector2((i * 719 % (width * 2)) - width * 0.25f, (i * 331 % (height * 2)) - height * 0.25f);
                    Assert.That(RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, point, null, out var expected), Is.True);
                    Assert.That(Vector2.Distance(projection.ToLocal(point), expected), Is.LessThan(0.01f));
                }
                Assert.That(OverlayProjection.TryCreate(rect, Vector2.zero, out _), Is.False);
                parent.transform.rotation = Quaternion.Euler(7f, 12f, 31f);
                Assert.That(OverlayProjection.TryCreate(rect, new Vector2(width, height), out _), Is.False,
                    "Tilted overlays retain Unity's per-point intersection test.");
            } finally { Object.DestroyImmediate(parent); }
        }

        [Test]
        public void HealthLabelsOwnANestedCanvasWithoutExtraScalerOrRaycaster() {
            var root = new GameObject("UI test");
            try {
                ArenaUiBuilder.Create(root.transform);
                var labels = root.transform.Find("GameCanvas/EnemyLabels");
                Assert.That(labels.GetComponent<Canvas>(), Is.Not.Null);
                Assert.That(labels.GetComponent<Canvas>().overrideSorting, Is.False);
                Assert.That(labels.GetComponent<CanvasScaler>(), Is.Null);
                Assert.That(labels.GetComponent<GraphicRaycaster>(), Is.Null);
                Assert.That(root.GetComponentsInChildren<CanvasScaler>(true).Length, Is.EqualTo(1));
            } finally { Object.DestroyImmediate(root); }
        }
    }
}

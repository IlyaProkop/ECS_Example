using Game.UI.Layout;
using System;
using System.Collections.Generic;
using Game.Config;
using Game.Domain;
using Game.Input;
using Game.UI;
using UnityEngine;
using Unity.Profiling;
using static UnityEngine.Object;

namespace Game.SceneFlow {
    // Owns only the procedurally built scene and its material instances.
    internal sealed class ArenaSceneBuilder : IDisposable {
        private static readonly ProfilerMarker CreateMarker = new ProfilerMarker("Arena.Startup.Scene");
        private static readonly ProfilerMarker EnvironmentMarker = new ProfilerMarker("Arena.Startup.Environment");
        private static readonly ProfilerMarker UiMarker = new ProfilerMarker("Arena.Startup.SceneUI");
        private readonly GameConfig config;
        private readonly Transform root;
        private Transform staticRoot, runtimeRoot, uiRoot;
        private Camera gameCamera;
        private PlayerInputAdapter inputAdapter;
        private ArenaUiViews ui;
        private GameObject arenaGate, entryMarker;
        private GameObject preparation;
        private readonly List<Material> environmentMaterials = new List<Material>();
        private SessionPhase lastPhase = (SessionPhase)(-1);
        private bool disposed;
        public Transform RuntimeRoot => this.runtimeRoot;
        public Camera Camera => this.gameCamera;
        public PlayerInputAdapter Input => this.inputAdapter;
        public HudView Hud => this.ui.Hud;
        public ResultsView Results => this.ui.Results;
        public EnemyHealthUiPresenter EnemyHealthUi => this.ui.EnemyHealth;

        public ArenaSceneBuilder(Transform host, GameConfig config) {
            using var marker = CreateMarker.Auto();
            this.config = config;
            this.root = new GameObject("ArenaScene").transform;
            this.root.SetParent(host, false);
            try {
                this.EnsureRoots();
                using (EnvironmentMarker.Auto()) { this.BuildStaticEnvironment(); this.BuildArena(); }
                this.BuildCamera();
                this.BuildInput();
                UiElements.EnsureEventSystem(this.staticRoot);
                using (UiMarker.Auto()) this.ui = ArenaUiBuilder.Create(this.uiRoot);
            } catch { this.Dispose(); throw; }
        }
        public void ResetSession() {
            this.gameCamera.transform.position = new Vector3(0f, this.config.cameraHeight, 0f);
            this.gameCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            this.lastPhase = (SessionPhase)(-1);
            this.RenderPhase(SessionPhase.AwaitingEntry);
        }
        public void ShowPreparation(bool visible) {
            if (visible && this.preparation == null) {
                var canvas = UiElements.CreateCanvas("ArenaPreparation", this.uiRoot);
                this.preparation = canvas.gameObject;
                var label = UiElements.CreateText("PreparationLabel", canvas.transform,
                    Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"), TextAnchor.MiddleCenter, 32, Color.white,
                    "Подготовка арены…");
                UiElements.SetAnchor(label.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
            }
            this.ui.Hud.gameObject.SetActive(!visible);
            if (this.preparation != null) this.preparation.SetActive(visible);
        }
        public void RenderPhase(SessionPhase phase) {
            if (phase == this.lastPhase) return;
            this.lastPhase = phase;
            this.arenaGate.SetActive(phase == SessionPhase.Combat || phase == SessionPhase.Results || phase == SessionPhase.Meta);
            this.entryMarker.SetActive(phase == SessionPhase.AwaitingEntry);
        }
        public void Dispose() {
            if (this.disposed) return;
            this.disposed = true;
            foreach (var material in this.environmentMaterials) Destroy(material);
            if (this.root != null) Destroy(this.root.gameObject);
        }

        private void EnsureRoots() {
            this.staticRoot = this.EnsureChildRoot("StaticRoot");
            this.runtimeRoot = this.EnsureChildRoot("RuntimeRoot");
            this.uiRoot = this.EnsureChildRoot("UiRoot");
        }

        private Transform EnsureChildRoot(string name) {
            var go = new GameObject(name);
            go.transform.SetParent(this.root, false);
            return go.transform;
        }

        private void BuildStaticEnvironment() {
            this.ClearChildren(this.staticRoot);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(this.staticRoot, false);
            ground.transform.position = Vector3.zero;
            var planeScale = this.config.groundSize / 10f;
            ground.transform.localScale = new Vector3(planeScale, 1f, planeScale);
            this.ColorPrimitive(ground, new Color(0.13f, 0.17f, 0.22f));

            var obstaclesRoot = new GameObject("Obstacles").transform;
            obstaclesRoot.SetParent(this.staticRoot, false);

            for (var i = 0; i < this.config.obstaclePositions.Length; i++) {
                var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = $"Obstacle_{i}";
                obstacle.transform.SetParent(obstaclesRoot, false);
                var scale = this.config.obstacleScale;
                obstacle.transform.localScale = scale;
                var position = this.config.obstaclePositions[i];
                obstacle.transform.position = new Vector3(position.x, scale.y * 0.5f, position.z);
                this.ColorPrimitive(obstacle, new Color(0.4f, 0.47f, 0.55f));
            }
        }

        private void ColorPrimitive(GameObject target, Color color) {
            var template = Resources.Load<Material>("ArenaMaterial");
            var material = template != null ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            target.GetComponent<Renderer>().sharedMaterial = material;
            this.environmentMaterials.Add(material);
            var collider = target.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        private GameObject ArenaBlock(string name, Vector3 position, Vector3 scale, Color color) {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(this.staticRoot, false);
            block.transform.position = position;
            block.transform.localScale = scale;
            this.ColorPrimitive(block, color);
            return block;
        }

        private void BuildArena() {
            var half = this.config.arenaHalfSize;
            var wall = new Color(0.22f, 0.35f, 0.42f);
            this.ArenaBlock("ArenaFloor", new Vector3(0f, 0.01f, 0f), new Vector3(half.x * 2f, 0.02f, half.y * 2f), new Color(0.19f, 0.25f, 0.31f));
            this.ArenaBlock("NorthWall", new Vector3(0f, 0.6f, half.y + 0.2f), new Vector3(half.x * 2f + 0.8f, 1.2f, 0.4f), wall);
            this.ArenaBlock("WestWall", new Vector3(-half.x - 0.2f, 0.6f, 0f), new Vector3(0.4f, 1.2f, half.y * 2f), wall);
            this.ArenaBlock("EastWall", new Vector3(half.x + 0.2f, 0.6f, 0f), new Vector3(0.4f, 1.2f, half.y * 2f), wall);
            var segmentWidth = half.x - 2f;
            for (var side = -1; side <= 1; side += 2) {
                this.ArenaBlock("SouthWall", new Vector3(side * (half.x + 2f) * 0.5f, 0.6f, -half.y - 0.2f),
                    new Vector3(segmentWidth, 1.2f, 0.4f), wall);
            }
            this.arenaGate = this.ArenaBlock("ArenaGate", new Vector3(0f, 0.6f, -half.y - 0.2f), new Vector3(4f, 1.2f, 0.4f), new Color(0.9f, 0.4f, 0.2f));
            this.arenaGate.SetActive(false);
            this.entryMarker = this.ArenaBlock("EntryZone", new Vector3(0f, 0.06f, -half.y + 0.75f), new Vector3(3f, 0.05f, 1f), new Color(0.25f, 0.75f, 0.45f));
        }

        private void BuildCamera() {
            var existingListeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (var i = 0; i < existingListeners.Length; i++) {
                existingListeners[i].enabled = false;
            }

            var cameraObject = new GameObject("GameCamera");
            cameraObject.transform.SetParent(this.staticRoot, false);

            this.gameCamera = cameraObject.AddComponent<Camera>();
            this.gameCamera.orthographic = true;
            this.gameCamera.orthographicSize = this.config.cameraOrthographicSize;
            this.gameCamera.nearClipPlane = 0.1f;
            this.gameCamera.farClipPlane = 200f;
            this.gameCamera.transform.position = new Vector3(0f, this.config.cameraHeight, 0f);
            this.gameCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            if (cameraObject.GetComponent<AudioListener>() == null) {
                cameraObject.AddComponent<AudioListener>();
            }

            var allCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (var i = 0; i < allCameras.Length; i++) {
                if (allCameras[i] != this.gameCamera) {
                    allCameras[i].enabled = false;
                }
            }
        }

        private void BuildInput() {
            var inputObject = new GameObject("PlayerInputAdapter");
            inputObject.transform.SetParent(this.staticRoot, false);
            this.inputAdapter = inputObject.AddComponent<PlayerInputAdapter>();
        }

        private void ClearChildren(Transform root) {
            for (var i = root.childCount - 1; i >= 0; i--) {
                var child = root.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

    }
}

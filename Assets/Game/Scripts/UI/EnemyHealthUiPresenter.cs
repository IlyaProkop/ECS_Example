using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI {
    // Unity view over a bounded set of stable slots. Only Prewarm creates label objects.
    public sealed class EnemyHealthUiPresenter : MonoBehaviour {
        [SerializeField] private RectTransform canvasRect;
        [SerializeField] private Font font;
        [SerializeField] private int fontSize = 18;
        private LabelState[] labels = Array.Empty<LabelState>();
        private HealthLabelSlots slots = new HealthLabelSlots(0);
        public int Capacity => this.labels.Length;

        public void Initialize(RectTransform canvasRoot, Font customFont = null, int size = 18) {
            if (this.labels.Length != 0) throw new InvalidOperationException("Initialize HP view before prewarming.");
            this.canvasRect = canvasRoot;
            this.font = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            this.fontSize = size;
        }

        public void Prewarm(int capacity) {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (this.canvasRect == null) throw new InvalidOperationException("HP view needs an initialized canvas root.");
            this.ClearAll();
            if (capacity == this.labels.Length) return;
            var resized = new LabelState[capacity];
            Array.Copy(this.labels, resized, Math.Min(capacity, this.labels.Length));
            for (var i = capacity; i < this.labels.Length; i++) Release(this.labels[i].Text);
            var oldCount = this.labels.Length;
            this.labels = resized;
            this.slots = new HealthLabelSlots(capacity);
            for (var i = oldCount; i < capacity; i++) this.labels[i] = this.CreateLabel();
        }

        // Input is the complete, preselected visible set for an overlay canvas.
        internal void Apply(ReadOnlySpan<EnemyHealthLabel> requests) {
            this.slots.Bind(requests);
            var projection = default(OverlayProjection);
            var projected = requests.Length > 3 && OverlayProjection.TryCreate(this.canvasRect,
                new Vector2(Screen.width, Screen.height), out projection);
            for (var slot = 0; slot < this.labels.Length; slot++) {
                ref var state = ref this.labels[slot];
                var item = this.slots.ItemAt(slot);
                if (item < 0) { SetVisible(ref state, false); continue; }
                ref readonly var request = ref requests[item];
                var local = projected ? projection.ToLocal(request.ScreenPosition) : default;
                if (!projected && !RectTransformUtility.ScreenPointToLocalPointInRectangle(this.canvasRect, request.ScreenPosition, null, out local)) {
                    SetVisible(ref state, false);
                    continue;
                }
                var health = Mathf.CeilToInt(request.Health);
                if (health != state.Health) { state.Text.text = health.ToString(); state.Health = health; }
                if (!state.HasPosition || !state.Position.Equals(local)) {
                    state.Rect.anchoredPosition = local;
                    state.Position = local; state.HasPosition = true;
                }
                SetVisible(ref state, true);
            }
        }

        public void ClearAll() {
            this.slots.Clear();
            for (var i = 0; i < this.labels.Length; i++) SetVisible(ref this.labels[i], false);
        }

        private LabelState CreateLabel() {
            var go = new GameObject("EnemyHpLabel", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(this.canvasRect, false);
            var label = go.GetComponent<Text>();
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.font = this.font;
            label.fontSize = this.fontSize;
            label.color = Color.white;
            label.raycastTarget = false;
            go.SetActive(false);
            return new LabelState { Text = label, Rect = label.rectTransform, Health = int.MinValue };
        }

        private static void SetVisible(ref LabelState state, bool visible) {
            if (state.Visible == visible || state.Text == null) return;
            state.Text.gameObject.SetActive(visible);
            state.Visible = visible;
        }
        private void OnDisable() => this.ClearAll();
        private void OnDestroy() {
            foreach (var state in this.labels) Release(state.Text);
            this.labels = Array.Empty<LabelState>();
        }
        private static void Release(Text label) {
            if (label == null) return;
            if (Application.isPlaying) Destroy(label.gameObject);
            else DestroyImmediate(label.gameObject);
        }

        private struct LabelState {
            public Text Text;
            public RectTransform Rect;
            public int Health;
            public Vector2 Position;
            public bool Visible, HasPosition;
        }
    }
}

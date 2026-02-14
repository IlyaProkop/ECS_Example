using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Game.UI.Layout {
    // Shared construction primitives. Layout builders contain no game state or subscriptions.
    internal static class UiElements {
        public static Canvas CreateCanvas(string name, Transform parent) {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }
        public static Text CreateText(string name, Transform parent, Font font, TextAnchor anchor, int size, Color color, string value = "") {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = font; text.alignment = anchor; text.fontSize = size; text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }
        public static Button CreateButton(string name, Transform parent, Font font, string label, int fontSize = 28) {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f, 1f);
            var text = CreateText("Label", go.transform, font, TextAnchor.MiddleCenter, fontSize, Color.white, label);
            SetAnchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return go.GetComponent<Button>();
        }
        public static void SetAnchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size) {
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        public static void EnsureEventSystem(Transform parent) {
            var existing = Object.FindFirstObjectByType<EventSystem>();
            var go = existing != null ? existing.gameObject : new GameObject("EventSystem", typeof(EventSystem));
            if (existing == null) go.transform.SetParent(parent, false);
            var module = go.GetComponent<BaseInputModule>();
            if (module is InputSystemUIInputModule) return;
            if (module != null) { module.enabled = false; Object.Destroy(module); }
            SceneUiInputScope.Attach(go);
        }
    }
}

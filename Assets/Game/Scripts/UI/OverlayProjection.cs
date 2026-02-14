using UnityEngine;

namespace Game.UI {
    // An overlay parallel to the screen has an affine mapping, including 2D scale/rotation.
    // Tilted planes use Unity's per-point ray test; large bases avoid pixel cancellation.
    internal readonly struct OverlayProjection {
        private readonly Vector2 origin, xAxis, yAxis;
        private OverlayProjection(Vector2 origin, Vector2 xAxis, Vector2 yAxis) {
            this.origin = origin; this.xAxis = xAxis; this.yAxis = yAxis;
        }
        public Vector2 ToLocal(Vector2 point) => this.origin + this.xAxis * point.x + this.yAxis * point.y;
        public static bool TryCreate(RectTransform root, Vector2 screenSize, out OverlayProjection projection) {
            projection = default;
            if (screenSize.x <= 0 || screenSize.y <= 0 || Mathf.Abs(root.forward.z) < 0.99999f ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, Vector2.zero, null, out var origin) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(screenSize.x, 0), null, out var right) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(0, screenSize.y), null, out var top)) return false;
            projection = new OverlayProjection(origin, (right - origin) / screenSize.x, (top - origin) / screenSize.y);
            return true;
        }
    }
}

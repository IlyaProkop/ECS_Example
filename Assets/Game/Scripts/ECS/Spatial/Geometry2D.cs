using UnityEngine;

namespace Game.Spatial {
    internal static class Geometry2D {
        private const float Epsilon = 0.000001f;

        public static bool SegmentIntersectsCircle(Vector2 start, Vector2 end, Vector2 center, float radius, out float hitT) {
            var offset = start - center;
            var c = Vector2.Dot(offset, offset) - radius * radius;
            hitT = 0f;
            if (c <= 0f) return true;
            var direction = end - start;
            var a = Vector2.Dot(direction, direction);
            if (a <= Epsilon) return false;
            var b = Vector2.Dot(offset, direction);
            var discriminant = b * b - a * c;
            if (discriminant < 0f) return false;
            hitT = (-b - Mathf.Sqrt(discriminant)) / a;
            return hitT >= 0f && hitT <= 1f;
        }

        public static bool SegmentIntersectsAabb2D(
            Vector2 start,
            Vector2 end,
            float minX,
            float maxX,
            float minY,
            float maxY) {
            return SegmentIntersectsAabb2D(start, end, minX, maxX, minY, maxY, out _);
        }

        public static bool SegmentIntersectsAabb2D(
            Vector2 start,
            Vector2 end,
            float minX,
            float maxX,
            float minY,
            float maxY,
            out float hitT) {
            var direction = end - start;
            var tMin = 0f;
            var tMax = 1f;

            if (!ClipSegmentAxis(start.x, direction.x, minX, maxX, ref tMin, ref tMax) ||
                !ClipSegmentAxis(start.y, direction.y, minY, maxY, ref tMin, ref tMax)) {
                hitT = 0f;
                return false;
            }

            hitT = Mathf.Clamp01(tMin);
            return true;
        }

        private static bool ClipSegmentAxis(
            float start,
            float direction,
            float min,
            float max,
            ref float tMin,
            ref float tMax) {
            if (Mathf.Abs(direction) <= Epsilon) {
                return start >= min && start <= max;
            }

            var invDirection = 1f / direction;
            var t1 = (min - start) * invDirection;
            var t2 = (max - start) * invDirection;
            if (t1 > t2) {
                var temp = t1;
                t1 = t2;
                t2 = temp;
            }

            tMin = Mathf.Max(tMin, t1);
            tMax = Mathf.Min(tMax, t2);
            return tMin <= tMax;
        }
    }
}

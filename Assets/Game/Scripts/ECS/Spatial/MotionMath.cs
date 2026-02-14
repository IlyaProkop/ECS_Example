using Unity.Mathematics;

namespace Game.Spatial {
    // Explicit rounding boundaries for the Mono reference and Burst. A sum of float
    // products must not round each product before accumulation in the Burst backend.
    internal static class MotionMath {
        public static float SquaredLength(float3 value) => (float)SumSquares(value);
        public static float SquaredLength(float2 value) =>
            (float)((double)value.x * value.x + (double)value.y * value.y);
        public static float Length(float3 value) => (float)math.sqrt(SumSquares(value));
        private static double SumSquares(float3 value) =>
            (double)value.x * value.x + (double)value.y * value.y + (double)value.z * value.z;
    }
}

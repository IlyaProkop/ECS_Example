using System;

namespace Game.Domain {
    public static class SimulationLimits { public const int MaxStepsPerFrame = 8; }

    // Conservative byte allowance for BOTH damage arrays and ability cast/event arrays.
    // This is a combat-buffer guard, not an estimate of the complete Unity process.
    public static class CombatBufferBudget {
        public const long MaxBytes = 64L * 1024 * 1024;
        public static long EstimateBytes(long damageCapacity, int castCapacity) =>
            checked(damageCapacity * 128L + castCapacity * (64L + 64L * SimulationLimits.MaxStepsPerFrame) + 4096L);
        public static int Validate(long damageCapacity, int castCapacity) {
            var bytes = EstimateBytes(damageCapacity, castCapacity);
            if (damageCapacity < 0 || damageCapacity > int.MaxValue || bytes > MaxBytes)
                throw new ArgumentException($"Combat buffers require approximately {bytes / (1024d * 1024d):0.0} MiB; limit is 64 MiB. Reduce population, ability slots or effect damage capacity.");
            return (int)damageCapacity;
        }
    }
}

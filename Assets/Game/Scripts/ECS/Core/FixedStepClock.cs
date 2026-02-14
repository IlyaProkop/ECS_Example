using System;

namespace Game.ECS.Core {
    // Scheduling policy only: no world, input, gameplay or presentation dependencies.
    internal sealed class FixedStepClock {
        private readonly double step;
        private readonly int maxSteps;
        private double accumulator;
        private int completedSteps;

        public FixedStepClock(float step, int maxSteps) {
            ValidateElapsed(step);
            if (step == 0f) throw new ArgumentOutOfRangeException(nameof(step));
            if (maxSteps <= 0) throw new ArgumentOutOfRangeException(nameof(maxSteps));
            this.step = step;
            this.maxSteps = maxSteps;
        }

        public double DroppedSeconds { get; private set; }
        public float InterpolationAlpha => (float)Math.Max(0d, Math.Min(1d, this.accumulator / this.step));
        public bool HasStep => this.accumulator + 1e-9 >= this.step && this.completedSteps < this.maxSteps;

        public static void ValidateElapsed(float elapsedSeconds) {
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds) || elapsedSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        public void BeginFrame(float elapsedSeconds) {
            ValidateElapsed(elapsedSeconds);
            this.accumulator += elapsedSeconds;
            this.completedSteps = 0;
        }

        // Commit time only after the simulation step succeeds.
        public void CompleteStep() {
            this.accumulator -= this.step;
            this.completedSteps++;
        }

        public void EndFrame() {
            if (this.accumulator < this.step) return;
            var dropped = Math.Floor(this.accumulator / this.step) * this.step;
            this.accumulator -= dropped;
            this.DroppedSeconds += dropped;
        }
    }
}

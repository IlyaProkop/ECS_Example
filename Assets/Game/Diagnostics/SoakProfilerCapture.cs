using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace Game.Diagnostics {
    // Opt-in attribution capture. Its timings are not a performance comparison.
    internal sealed class SoakProfilerCapture : IDisposable {
        private bool active;
        public SoakProfilerCapture(string path) {
            if (!Debug.isDebugBuild) throw new InvalidOperationException("Allocation callstacks require a Development player.");
            if (Profiler.enabled) throw new InvalidOperationException("Another profiler capture is already active.");
            Profiler.logFile = path;
            Profiler.maxUsedMemory = 256 * 1024 * 1024;
            Profiler.enableBinaryLog = true;
            Profiler.enableAllocationCallstacks = true;
            Profiler.enabled = true;
            this.active = true;
        }
        public void Update(double elapsedSeconds) { if (elapsedSeconds >= 8) this.Dispose(); }
        public void Dispose() {
            if (!this.active) return;
            this.active = false;
            Profiler.enabled = false;
            Profiler.enableAllocationCallstacks = false;
            Profiler.enableBinaryLog = false;
            Profiler.logFile = string.Empty;
        }
    }
}

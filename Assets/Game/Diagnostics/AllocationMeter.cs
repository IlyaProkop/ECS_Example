using System;
using Unity.Profiling;

namespace Game.Diagnostics {
    // Unity Mono can expose a no-op GC byte counter. Calibrate before interpreting zero.
    public sealed class AllocationMeter : IDisposable {
        private ProfilerRecorder recorder;
        private long before;
        public bool ByteCounterSupported { get; }
        public bool EventCounterSupported { get; }
        public bool Supported => this.ByteCounterSupported || this.EventCounterSupported;
        public long Bytes { get; private set; } = -1;
        public long Events { get; private set; } = -1;
        public AllocationMeter() {
            this.recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 262144, ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            if (this.recorder.Valid) this.recorder.Start();
            var before = GC.GetAllocatedBytesForCurrentThread(); var probe = new byte[65536];
            this.ByteCounterSupported = GC.GetAllocatedBytesForCurrentThread() - before >= 65536;
            GC.KeepAlive(probe); if (this.recorder.Valid) this.recorder.Stop();
            this.EventCounterSupported = this.recorder.Valid && this.ReadEvents() > 0;
        }
        public void Start() {
            if (this.recorder.Valid) { this.recorder.Reset(); this.recorder.Start(); }
            this.before = GC.GetAllocatedBytesForCurrentThread();
        }
        public void Stop() {
            var bytes = GC.GetAllocatedBytesForCurrentThread() - this.before;
            if (this.recorder.Valid) this.recorder.Stop();
            this.Bytes = this.ByteCounterSupported ? bytes : -1;
            this.Events = this.EventCounterSupported ? this.ReadEvents() : -1;
        }
        private long ReadEvents() {
            if (this.recorder.Count == this.recorder.Capacity) throw new InvalidOperationException("Allocation recorder capacity exhausted.");
            var count = 0L;
            for (var i = 0; i < this.recorder.Count; i++) count += this.recorder.GetSample(i).Count;
            return count;
        }
        public void Dispose() => this.recorder.Dispose();
    }
}

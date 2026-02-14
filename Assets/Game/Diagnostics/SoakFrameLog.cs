using System;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace Game.Diagnostics {
    // Fixed-size raw samples; no formatting, sorting or file IO in the measured loop.
    internal sealed class SoakFrameLog : IDisposable {
        private readonly Counter main, render, gpu, allocated, collect, incremental, batches;
        private readonly Sample[] samples;
        private int count;
        public readonly string[] AvailableGcMarkers;
        public int Count => this.count;
        public bool AllocationCounterAvailable => this.allocated.Available;
        public bool CollectionMarkerAvailable => this.collect.Available;
        public bool IncrementalMarkerAvailable => this.incremental.Available;
        public long Allocated => this.allocated.Read();
        public long Batches => this.batches.Read();

        public SoakFrameLog(int capacity) {
            this.samples = new Sample[capacity];
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var descriptions = new Dictionary<string, ProfilerRecorderDescription>();
            var gcNames = new List<string>();
            foreach (var handle in handles) {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                descriptions[description.Name] = description;
                if (description.Name.StartsWith("GC.", StringComparison.Ordinal) || description.Name.Contains("GarbageCollector"))
                    gcNames.Add(description.Name);
            }
            this.AvailableGcMarkers = gcNames.ToArray();
            this.main = new Counter(descriptions, "Main Thread");
            this.render = new Counter(descriptions, "Render Thread");
            this.gpu = new Counter(descriptions, "GPU Frame Time");
            this.allocated = new Counter(descriptions, "GC Allocated In Frame");
            this.collect = new Counter(descriptions, "GC.Collect");
            this.incremental = new Counter(descriptions, "GarbageCollector.CollectIncremental");
            this.batches = new Counter(descriptions, "Batches Count");
        }
        public void Add(double seconds, double wallMilliseconds, int stage, int cycle, int collections, double droppedSeconds) {
            if (this.count == this.samples.Length) throw new InvalidOperationException("Soak frame capacity exceeded.");
            this.samples[this.count++] = new Sample { seconds = seconds, wall = (float)wallMilliseconds,
                main = this.main.Milliseconds, render = this.render.Milliseconds, gpu = this.gpu.Milliseconds,
                allocated = this.allocated.Read(), collect = this.collect.Milliseconds, incremental = this.incremental.Milliseconds,
                stage = stage, cycle = cycle, collections = collections, droppedSeconds = (float)droppedSeconds };
        }
        public void Write(string path) {
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(0x534F414B); writer.Write(1); writer.Write(this.count);
            for (var i = 0; i < this.count; i++) {
                ref var s = ref this.samples[i];
                writer.Write(s.seconds); writer.Write(s.wall); writer.Write(s.main); writer.Write(s.render); writer.Write(s.gpu);
                writer.Write(s.allocated); writer.Write(s.collect); writer.Write(s.incremental);
                writer.Write(s.stage); writer.Write(s.cycle); writer.Write(s.collections); writer.Write(s.droppedSeconds);
            }
        }
        public void Dispose() {
            this.main.Dispose(); this.render.Dispose(); this.gpu.Dispose(); this.allocated.Dispose();
            this.collect.Dispose(); this.incremental.Dispose(); this.batches.Dispose();
        }
        private struct Sample {
            public double seconds;
            public float wall, main, render, gpu, collect, incremental, droppedSeconds;
            public long allocated;
            public int stage, cycle, collections;
        }
        private sealed class Counter : IDisposable {
            private ProfilerRecorder recorder;
            public bool Available { get; }
            public Counter(Dictionary<string, ProfilerRecorderDescription> descriptions, string name) {
                if (!descriptions.TryGetValue(name, out var description)) return;
                this.recorder = ProfilerRecorder.StartNew(description.Category, name, 1);
                this.Available = this.recorder.Valid;
            }
            public long Read() => this.Available && this.recorder.Count > 0 ? this.recorder.LastValue : -1;
            public float Milliseconds { get { var value = this.Read(); return value < 0 ? -1f : value / 1000000f; } }
            public void Dispose() { if (this.Available) this.recorder.Dispose(); }
        }
    }
}

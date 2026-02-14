using System;
using System.Collections.Generic;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;

namespace Game.Diagnostics {
    // Diagnostic-only storage. Allocate before sampling; query the preceding completed frame.
    internal sealed class FrameMetrics : IDisposable {
        private readonly Metric[] metrics;
        public FrameMetrics(int capacity) {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var selected = new List<Metric>();
            foreach (var handle in handles) {
                var description = ProfilerRecorderHandle.GetDescription(handle);
                var name = description.Name;
                if (name.StartsWith("Arena.", StringComparison.Ordinal) || name.StartsWith("Canvas.", StringComparison.Ordinal) ||
                    name == "UI.Layout" || name == "UI.Render" || name == "Main Thread" || name == "Render Thread" ||
                    name == "GC Allocated In Frame" || name == "GC Used Memory" || name == "Batches Count" ||
                    name == "SetPass Calls Count" || name == "Triangles Count" || name.EndsWith("Frame Time", StringComparison.Ordinal))
                    selected.Add(new Metric(name, description.Category, description.UnitType.ToString(), capacity));
            }
            this.metrics = selected.ToArray();
        }
        public long LastValue(string name) {
            foreach (var metric in this.metrics)
                if (metric.Name == name && metric.Recorder.Valid && metric.Recorder.Count > 0) return metric.Recorder.LastValue;
            return -1;
        }
        public void Sample() {
            foreach (var metric in this.metrics) {
                if (metric.Recorder.Valid && metric.Recorder.Count > 0)
                    metric.Values[metric.Count++] = metric.Recorder.LastValue;
            }
        }
        public Result[] Read() {
            var result = new Result[this.metrics.Length];
            for (var i = 0; i < result.Length; i++) {
                var metric = this.metrics[i];
                result[i] = new Result { name = metric.Name, unit = metric.Unit, values = Distribution.Read(metric.Values, metric.Count) };
            }
            return result;
        }
        public void Dispose() { foreach (var metric in this.metrics) metric.Recorder.Dispose(); }
        private sealed class Metric {
            public readonly string Name, Unit;
            public ProfilerRecorder Recorder;
            public readonly double[] Values;
            public int Count;
            public Metric(string name, ProfilerCategory category, string unit, int capacity) {
                this.Name = name; this.Unit = unit; this.Values = new double[capacity];
                this.Recorder = ProfilerRecorder.StartNew(category, name, 1);
            }
        }
        [Serializable] internal sealed class Result { public string name, unit; public Distribution values; }
    }

    [Serializable] internal sealed class Distribution {
        public int samples;
        public double median = -1, p95 = -1, p99 = -1, max = -1, mean = -1;
        public static Distribution Read(double[] values, int count) {
            var result = new Distribution { samples = count };
            if (count == 0) return result;
            double sum = 0;
            for (var i = 0; i < count; i++) sum += values[i];
            Array.Sort(values, 0, count);
            result.median = values[count / 2]; result.p95 = values[Math.Min(count - 1, (int)(count * 0.95))];
            result.p99 = values[Math.Min(count - 1, (int)(count * 0.99))];
            result.max = values[count - 1]; result.mean = sum / count;
            return result;
        }
    }
}

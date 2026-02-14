using System;
using System.Collections.Generic;
using System.Diagnostics;
using Scellecs.Morpeh;

namespace Game.Diagnostics {
    // Installed only by the opt-in benchmark. Ordinary sessions retain their original systems.
    internal sealed class SystemTimings {
        private readonly List<TimedSystem> systems = new List<TimedSystem>();
        private readonly int sampleCount;
        public bool Recording { get; set; }

        public SystemTimings(int sampleCount) => this.sampleCount = sampleCount;

        public ISystem Decorate(ISystem system) {
            var timed = new TimedSystem(this, system, this.sampleCount);
            this.systems.Add(timed);
            return timed;
        }

        public Entry[] Read() {
            var result = new Entry[this.systems.Count];
            for (var i = 0; i < result.Length; i++) result[i] = this.systems[i].Read(i);
            return result;
        }

        [Serializable] internal sealed class Entry {
            public int order, samples;
            public string system;
            public double meanMilliseconds, p95Milliseconds, maxMilliseconds;
        }

        private sealed class TimedSystem : ISystem {
            private readonly SystemTimings owner;
            private readonly ISystem inner;
            private readonly long[] ticks;
            private int count;
            public TimedSystem(SystemTimings owner, ISystem inner, int sampleCount) {
                this.owner = owner; this.inner = inner; this.ticks = new long[sampleCount];
            }
            public World World { get => this.inner.World; set => this.inner.World = value; }
            public void OnAwake() => this.inner.OnAwake();
            public void OnUpdate(float deltaTime) {
                if (!this.owner.Recording) { this.inner.OnUpdate(deltaTime); return; }
                var start = Stopwatch.GetTimestamp();
                this.inner.OnUpdate(deltaTime);
                this.ticks[this.count++] = Stopwatch.GetTimestamp() - start;
            }
            public Entry Read(int order) {
                long sum = 0;
                for (var i = 0; i < this.count; i++) sum += this.ticks[i];
                Array.Sort(this.ticks, 0, this.count);
                var scale = 1000.0 / Stopwatch.Frequency;
                return new Entry {
                    order = order, system = this.inner.GetType().FullName, samples = this.count,
                    meanMilliseconds = this.count == 0 ? 0 : sum * scale / this.count,
                    p95Milliseconds = this.count == 0 ? 0 : this.ticks[(int)(this.count * 0.95)] * scale,
                    maxMilliseconds = this.count == 0 ? 0 : this.ticks[this.count - 1] * scale
                };
            }
            public void Dispose() => this.inner.Dispose();
        }
    }
}

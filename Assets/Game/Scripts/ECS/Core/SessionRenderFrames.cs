using System;
using Game.Domain;
using Unity.Profiling;

namespace Game.ECS.Core {
    // Presentation projection of authoritative snapshots. Owns capture/history storage;
    // has no clock, phase transitions, world updates or gameplay commands.
    internal sealed class SessionRenderFrames {
        private static readonly ProfilerMarker CaptureMarker = new ProfilerMarker("Arena.Interpolation.Capture");
        private readonly ActorInterpolationBuffer history;
        private readonly ActorView[] capture;

        public SessionRenderFrames(int capacity) {
            this.history = new ActorInterpolationBuffer(capacity);
            this.capture = new ActorView[capacity];
        }

        public void Capture(SessionReader reader, bool reset) {
            using var sample = CaptureMarker.Auto();
            var count = reader.CopyActors(this.capture);
            this.history.Capture(this.capture.AsSpan(0, count), reset);
        }

        public int CopyTo(Span<ActorView> destination, float alpha) => this.history.CopyTo(destination, alpha);
    }
}

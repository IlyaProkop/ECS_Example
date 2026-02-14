using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.ECS.Navigation {
    // Session owns a fixed slab. Enemy slots are monotonic and never recycled within a wave.
    internal sealed class PathStorage {
        private readonly Vector3[] waypoints;
        private readonly int slots;
        public int WaypointsPerPath { get; }
        public PathStorage(int slots, int waypointsPerPath) {
            if (slots < 1 || waypointsPerPath < 1) throw new ArgumentOutOfRangeException();
            this.slots = slots;
            this.WaypointsPerPath = waypointsPerPath;
            this.waypoints = new Vector3[checked(slots * waypointsPerPath)];
        }
        public int Write(int slot, List<Vector3> path, int start) {
            this.CheckSlot(slot);
            var count = Math.Min(this.WaypointsPerPath, path.Count - start);
            // Long routes keep a prefix. Consuming it triggers another bounded repath.
            path.CopyTo(start, this.waypoints, slot * this.WaypointsPerPath, count);
            return count;
        }
        public Vector3 Get(int slot, int waypoint) {
            this.CheckSlot(slot);
            if ((uint)waypoint >= this.WaypointsPerPath) throw new ArgumentOutOfRangeException(nameof(waypoint));
            return this.waypoints[slot * this.WaypointsPerPath + waypoint];
        }
        private void CheckSlot(int slot) {
            if ((uint)slot >= this.slots) throw new ArgumentOutOfRangeException(nameof(slot));
        }
    }
}

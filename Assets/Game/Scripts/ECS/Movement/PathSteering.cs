using Game.Spatial;
using Game.ECS.Components;
using Game.ECS.Navigation;
using Unity.Mathematics;
using UnityEngine;

namespace Game.ECS.Movement {
    // Resolves a navigation goal into velocity; has no knowledge of teams or the player.
    internal static class PathSteering {
        public static float3 DesiredVelocity(float3 position, float3 waypoint, float3 goal, float speed, float stoppingDistance) {
            if (MotionMath.SquaredLength(goal - position) <= (double)stoppingDistance * stoppingDistance) return float3.zero;
            var delta = waypoint - position;
            var length = MotionMath.Length(delta);
            return (length > 0.00001f ? delta / length : float3.zero) * speed;
        }
        public static Vector3 Resolve(Vector3 position, in MovementGoalComponent goal, float speed,
            ref EnemyPathComponent path, PathStorage storage, float squaredReach) {
            if (!goal.active) return Vector3.zero;
            var target = goal.position;
            while (path.currentWaypointIndex < path.waypointCount) {
                target = storage.Get(path.slot, path.currentWaypointIndex);
                if ((target - position).sqrMagnitude > squaredReach) break;
                path.currentWaypointIndex++;
                target = goal.position;
            }
            return DesiredVelocity(position, target, goal.position, speed, goal.stoppingDistance);
        }
    }
}

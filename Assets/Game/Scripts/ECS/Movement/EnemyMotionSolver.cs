using System;
using Game.Spatial;
using Unity.Mathematics;

namespace Game.ECS.Movement {
    internal readonly struct EnemyMotionInput {
        public readonly int ActorId;
        public readonly float3 Position, Velocity, DesiredVelocity;
        public readonly float Radius, Speed;

        public EnemyMotionInput(int actorId, float3 position, float3 velocity, float3 desiredVelocity,
            float radius, float speed) {
            this.ActorId = actorId; this.Position = position; this.Velocity = velocity; this.DesiredVelocity = desiredVelocity;
            this.Radius = radius; this.Speed = speed;
        }
    }

    internal readonly struct EnemyMotionFrame {
        public readonly float3 PlayerPosition;
        public readonly float PlayerRadius, MaxNeighborRadius, DeltaTime;

        public EnemyMotionFrame(float3 playerPosition, float playerRadius, float maxNeighborRadius, float deltaTime) {
            this.PlayerPosition = playerPosition; this.PlayerRadius = playerRadius;
            this.MaxNeighborRadius = maxNeighborRadius; this.DeltaTime = deltaTime;
        }
    }

    internal readonly struct EnemyMotionResult {
        public readonly float3 Position, Velocity;
        public EnemyMotionResult(float3 position, float3 velocity) { this.Position = position; this.Velocity = velocity; }
    }

    // Reads values, writes results. No World, Entity, stash, path mutation or callbacks.
    // Every actor reads the same pre-movement geometry, regardless of batch partitioning.
    internal static class EnemyMotionSolver {
        public static void Solve(ReadOnlySpan<EnemyMotionInput> inputs, Span<EnemyMotionResult> results,
            in EnemyMotionFrame frame, EnemyGeometryIndex.ReadView neighbors, float2 arenaHalfSize, in ObstacleGridLookupNative obstacles) {
            if (results.Length < inputs.Length) throw new ArgumentException("Motion output capacity is smaller than input.", nameof(results));
            for (var i = 0; i < inputs.Length; i++) {
                ref readonly var input = ref inputs[i];
                results[i] = SolveOne(input, frame,
                    neighbors.QueryCircle(input.Position, NeighborRange(input, frame)).GetEnumerator(), arenaHalfSize, obstacles);
            }
        }

        internal static float NeighborRange(in EnemyMotionInput input, in EnemyMotionFrame frame) {
            // Velocity can still exceed Speed after a debuff. Its L1 norm bounds travel
            // without a square root; include the radius-limited penetration correction.
            var velocityBound = math.abs((double)input.Velocity.x) + math.abs((double)input.Velocity.y) + math.abs((double)input.Velocity.z);
            var travel = math.max((double)input.Speed, velocityBound) * frame.DeltaTime + input.Radius;
            var playerContact = (double)input.Radius + frame.PlayerRadius;
            var playerReach = playerContact + travel;
            if (MotionMath.SquaredLength(frame.PlayerPosition - input.Position) <= playerReach * playerReach)
                travel += playerContact;
            // Each actor owns half the gap, so include twice its possible closing travel.
            return (float)(input.Radius + (double)frame.MaxNeighborRadius + 2d * travel + 0.1d);
        }

        internal static EnemyMotionResult SolveOne<T>(in EnemyMotionInput input, in EnemyMotionFrame frame,
            T neighbors, float2 arenaHalfSize, in ObstacleGridLookupNative obstacles) where T : struct, IEnemyGeometryEnumerator {
            var motion = new ActorMotionQuery(arenaHalfSize, input.Radius, true, obstacles);
            var start = input.Position;
            var constraints = neighbors;
            var velocity = MoveTowards(input.Velocity, input.DesiredVelocity, (float)((double)input.Speed * 18f * frame.DeltaTime));
            float3 next = motion.Move(start, velocity * frame.DeltaTime);
            // Evaluate penetration at the shared start-of-step snapshot. Applying one
            // contact before reading the next makes grid traversal order a force.
            var correction = double3.zero;


            while (neighbors.MoveNext()) {
                var other = neighbors.Current;
                if (other.ActorId == input.ActorId) continue;
                var point = other.Position;
                // Actor IDs are unique within a session; coincident centers get opposite directions.
                var side = input.ActorId < other.ActorId ? -1f : 1f;
                var offset = SeparationOffset(start, new float3(point.x, 0f, point.y), input.Radius + other.Radius, 0.5f, side);
                correction += (double3)offset;
            }
            // Keep contact pressure when surrounded: averaging by neighbor count lets
            // large crowds collapse. Bound the summed correction to one actor radius.
            var correctionLength = math.sqrt(math.lengthsq(correction));
            if (correctionLength > input.Radius) correction *= input.Radius / correctionLength;
            next += (float3)correction;
            next += SeparationOffset(next, frame.PlayerPosition, input.Radius + frame.PlayerRadius, 1f, 1f);
            next = motion.Move(start, next - start);
            var displacement = next - start;
            var fraction = 1d;
            while (constraints.MoveNext()) {
                var other = constraints.Current;
                if (other.ActorId == input.ActorId) continue;
                var delta = new double3(other.Position.x, 0d, other.Position.y) - (double3)start;
                var distance = math.sqrt(math.lengthsq(delta));
                if (distance <= 0.00001d) continue; // Stable-ID correction resolves coincident centers.
                var closing = math.dot((double3)displacement, delta) / distance;
                if (closing <= 0d) continue; // Tangential and separating motion remains available.
                var gap = math.max(0d, distance - input.Radius - other.Radius);
                fraction = math.min(fraction, gap * 0.5d / closing);
            }
            if (fraction < 1d) {
                var constrained = start + (float3)((double3)displacement * fraction);
                // A fraction of a slid displacement can intersect a corner. Reject that
                // position instead of projecting it again and invalidating the contact limit.
                next = motion.IsBlocked(constrained) ? start : constrained;
            }
            return new EnemyMotionResult(next, velocity);
        }

        private static float3 MoveTowards(float3 current, float3 target, float maxDelta) {
            var delta = target - current;
            var squared = MotionMath.SquaredLength(delta);
            if (squared == 0f || (maxDelta >= 0f && squared <= (double)maxDelta * maxDelta)) return target;
            var distance = (float)math.sqrt((double)squared);
            // Keep intermediate precision explicit across compiler backends.
            return (float3)((double3)current + (double3)delta / distance * maxDelta);
        }

        private static float3 SeparationOffset(float3 position, float3 other, float distance, float weight, float side) {
            var delta = position - other;
            delta.y = 0f;
            var squared = MotionMath.SquaredLength(delta);
            if (squared >= (double)distance * distance) return float3.zero;
            var length = (float)math.sqrt((double)squared);
            var direction = length > 0.00001f ? delta / length : new float3(side, 0f, 0f);
            return direction * (float)(((double)distance - length) * weight);
        }
    }
}

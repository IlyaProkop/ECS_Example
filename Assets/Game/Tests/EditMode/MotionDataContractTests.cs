using System;
using System.Reflection;
using Game.ECS.Movement;
using Game.Spatial;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Game.Tests {
    public sealed class MotionDataContractTests {
        [Test]
        public void JobAndNativePayloadsAreUnmanagedAndBlittable() {
            Check<EnemyMotionJob>();
            Check<EnemyMotionInput>();
            Check<EnemyMotionResult>();
            Check<EnemyMotionFrame>();
            Check<EnemyGeometrySample>();
            Check<NativeEnemyGeometrySnapshot.ReadView>();
            Check<ObstacleBounds>();
            Check<ObstacleGridLookupNative>();
        }

        // A compile-time constraint guards the full field graph, including nested containers.
        private static void Check<T>() where T : unmanaged {
            Assert.That(UnsafeUtility.IsBlittable<T>(), Is.True, typeof(T).FullName);
            Assert.That(ContainsReference(typeof(T)), Is.False, typeof(T).FullName);
        }

        [Test]
        public void ManagedSnapshotIsDistinctFromTheViewPassedToWorkers() {
            Assert.That(ContainsReference(typeof(EnemyGeometryIndex.ReadView)), Is.True);
            Assert.That(ContainsReference(typeof(NativeEnemyGeometrySnapshot.ReadView)), Is.False);
            Assert.That(ContainsReference(typeof(NativeEnemyGeometrySnapshot.Enumerator)), Is.False);
        }

        private static bool ContainsReference(Type type) {
            if (type.IsPointer || type.IsPrimitive || type.IsEnum) return false;
            if (!type.IsValueType) return true;
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (ContainsReference(field.FieldType)) return true;
            return false;
        }
    }
}

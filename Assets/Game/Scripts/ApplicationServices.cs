using System.IO;
using Game.Domain;
using Game.Flow;
using Game.SceneFlow;
using Game.Progression;
using UnityEngine;

namespace Game {
    // Composition root shared by scene hosts. Business logic lives in an instance service.
    public static class ApplicationServices {
        private static ProgressService progress;
        private static GameFlowController flow;
        public static GameFlowController Flow => flow ??= new GameFlowController(Progress, new UnityGameNavigation());
        public static ProgressService Progress => progress ??= new ProgressService(
            new JsonProgressStore(Path.Combine(Application.persistentDataPath, "progress.json")));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() {
            flow?.Dispose();
            flow = null;
            progress = null;
        }

        // Configure an alternate host (for example a player diagnostic) before any scene
        // resolves application services. Active applications cannot be rebound underneath UI.
        internal static void ConfigureProgressStore(IProgressStore store) {
            if (flow != null || progress != null)
                throw new System.InvalidOperationException("Configure progress before application startup.");
            progress = new ProgressService(store);
        }

#if UNITY_EDITOR
        public static void SetProgressStoreForTests(IProgressStore store) {
            Reset();
            ConfigureProgressStore(store);
        }
#endif
    }
}

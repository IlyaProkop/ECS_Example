using System;
using System.Collections.Generic;
using System.IO;
using Game.Domain;
using Game.Flow;

namespace Game.Progression {
    // Application-scoped instance. No Unity lifecycle, global state, or IO in ECS.
    public sealed class ProgressService : IProgressService {
        private readonly IProgressStore store;
        private readonly PlayerProgress current;
        private readonly HashSet<string> handledThisRun = new HashSet<string>();
        private bool pendingSave;
        public string Status { get; private set; }
        public PlayerProgress Current => this.current.Copy();
        public bool HasPendingSave => this.pendingSave;

        public ProgressService(IProgressStore store) {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.current = (store.Load() ?? new PlayerProgress()).Copy();
            if (!string.IsNullOrEmpty(this.current.lastSessionId)) this.handledThisRun.Add(this.current.lastSessionId);
            this.Status = (store as IProgressStoreDiagnostics)?.LoadWarning;
        }
        public void Record(in BattleResult result) {
            if (!string.IsNullOrEmpty(result.sessionId) && !this.handledThisRun.Contains(result.sessionId) && this.current.Apply(result)) {
                this.handledThisRun.Add(result.sessionId);
                this.pendingSave = true;
            }
            this.Flush();
        }
        public void Flush() {
            if (!this.pendingSave) return;
            try {
                this.store.Save(this.current.Copy());
                this.pendingSave = false;
                this.Status = "Прогресс сохранён";
            } catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException) {
                this.Status = "Ошибка сохранения. Прогресс пока доступен только в этом запуске.";
            }
        }
    }
}

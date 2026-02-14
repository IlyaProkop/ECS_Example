using System;
using System.IO;
using Game.Domain;
using UnityEngine;

namespace Game.Progression {
    public sealed class JsonProgressStore : IProgressStore, IProgressStoreDiagnostics {
        private readonly string path;
        public string LoadWarning { get; private set; }

        public JsonProgressStore(string path) => this.path = path;

        public PlayerProgress Load() {
            this.LoadWarning = null;
            if (this.TryLoad(this.path, out var progress)) return progress;
            if (this.TryLoad(this.path + ".bak", out progress)) {
                this.LoadWarning = "Прогресс восстановлен из резервной копии.";
                return progress;
            }
            return new PlayerProgress();
        }

        private bool TryLoad(string source, out PlayerProgress progress) {
            progress = null;
            if (!File.Exists(source)) return false;
            try {
                progress = JsonUtility.FromJson<PlayerProgress>(File.ReadAllText(source));
                if (progress == null || progress.version != 1 || progress.currency < 0 || progress.battles < 0 ||
                    progress.victories < 0 || progress.victories > progress.battles || progress.totalKills < 0 ||
                    float.IsNaN(progress.bestVictorySeconds) || float.IsInfinity(progress.bestVictorySeconds) || progress.bestVictorySeconds < 0f) {
                    throw new InvalidDataException("Invalid progress data or unsupported save version.");
                }
                return true;
            } catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException) {
                this.LoadWarning = "Не удалось загрузить прогресс. Начата новая игра.";
                progress = null;
                return false;
            }
        }

        public void Save(PlayerProgress progress) {
            var directory = Path.GetDirectoryName(this.path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporary = this.path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(progress, true));
            if (File.Exists(this.path)) File.Replace(temporary, this.path, this.path + ".bak");
            else File.Move(temporary, this.path);
        }
    }
}

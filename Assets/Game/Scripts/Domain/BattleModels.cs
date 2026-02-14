using System;

namespace Game.Domain {
    public enum SessionPhase { Initialization, AwaitingEntry, Combat, Results, Meta }
    public enum BattleOutcome { None, Victory, Defeat }
    public enum Team { Neutral, Player, Enemy }
    public enum DamageKind { Projectile, Contact, Area, Periodic }

    [Serializable]
    public struct BattleStatistics {
        public float duration;
        public float damageDealt;
        public float damageReceived;
        public int kills;
        public int attacks;
        public int abilityUses;
        public int criticalHits;
        public int coinsCollected;
    }

    public readonly struct BattleResult {
        public readonly string sessionId;
        public readonly BattleOutcome outcome;
        public readonly BattleStatistics statistics;
        public readonly int reward;

        public BattleResult(string sessionId, BattleOutcome outcome, BattleStatistics statistics, int reward) {
            this.sessionId = sessionId;
            this.outcome = outcome;
            this.statistics = statistics;
            this.reward = reward;
        }
    }

    [Serializable]
    public sealed class PlayerProgress {
        public int version = 1;
        public int currency;
        public int battles;
        public int victories;
        public int totalKills;
        public float bestVictorySeconds;
        public string lastSessionId;

        public PlayerProgress Copy() => (PlayerProgress)this.MemberwiseClone();

        // Called once per result at the application boundary, never from a hot system.
        public bool Apply(in BattleResult result) {
            if (result.outcome == BattleOutcome.None || string.IsNullOrEmpty(result.sessionId) ||
                this.lastSessionId == result.sessionId) {
                return false;
            }

            this.lastSessionId = result.sessionId;
            this.battles++;
            this.totalKills += result.statistics.kills;
            if (result.outcome == BattleOutcome.Victory) {
                this.victories++;
                this.currency += Math.Max(0, result.reward);
                if (this.bestVictorySeconds <= 0f || result.statistics.duration < this.bestVictorySeconds) {
                    this.bestVictorySeconds = result.statistics.duration;
                }
            }
            return true;
        }
    }

    public interface IProgressStoreDiagnostics {
        string LoadWarning { get; }
    }

    public interface IProgressStore {
        PlayerProgress Load();
        void Save(PlayerProgress progress);
    }
}

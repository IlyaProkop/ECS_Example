using UnityEngine;

namespace Game.Domain {
    // Value copies only. Holding or modifying a copy cannot alter the session.
    public readonly struct BattleSnapshot {
        public readonly int health, coins, enemiesRemaining;
        public readonly SessionPhase phase;
        public readonly BattleOutcome outcome;
        public readonly BattleStatistics statistics;
        public readonly float abilityCooldown;
        public readonly Abilities.AbilityBarSnapshot abilities;
        public readonly Vector3 playerPosition;
        public readonly Stats.PlayerStatsSnapshot playerStats;
        public bool isGameOver => this.phase == SessionPhase.Results || this.phase == SessionPhase.Meta;

        public BattleSnapshot(int health, int coins, int enemiesRemaining, SessionPhase phase,
            BattleOutcome outcome, BattleStatistics statistics, float abilityCooldown, Vector3 playerPosition,
            Stats.PlayerStatsSnapshot playerStats = default, Abilities.AbilityBarSnapshot abilities = default) {
            this.health = health; this.coins = coins; this.enemiesRemaining = enemiesRemaining;
            this.phase = phase; this.outcome = outcome; this.statistics = statistics;
            this.abilityCooldown = abilityCooldown; this.playerPosition = playerPosition;
            this.playerStats = playerStats; this.abilities = abilities;
        }
    }

    public enum ActorKind { Player, Enemy, Projectile, Coin }

    public readonly struct ActorView {
        // Monotonic session-local identity, never an ECS storage index or hash.
        public readonly int id;
        public readonly ActorKind kind;
        public readonly int visualId;
        public readonly Vector3 position;
        public readonly float radius, health;
        public ActorView(int id, ActorKind kind, Vector3 position, float radius, float health, int visualId = 0) {
            this.id = id; this.kind = kind; this.position = position; this.radius = radius; this.health = health;
            this.visualId = visualId == 0 ? ActorVisualIds.DefaultFor(kind) : visualId;
        }
    }
}

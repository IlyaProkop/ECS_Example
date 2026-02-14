namespace Game.Domain.Stats {
    // Aggregate HUD values; effect ownership/handles remain inside the simulation.
    public readonly struct PlayerStatsSnapshot {
        public readonly int ActiveEffects;
        public readonly float RemainingSeconds, MoveSpeed, Armor;
        public PlayerStatsSnapshot(int activeEffects, float remainingSeconds, float moveSpeed, float armor) {
            this.ActiveEffects = activeEffects; this.RemainingSeconds = remainingSeconds;
            this.MoveSpeed = moveSpeed; this.Armor = armor;
        }
    }
}

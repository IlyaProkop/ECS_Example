namespace Game.Domain.Abilities {
    // Unknown/custom effects retain the all-actor bound; hostile-only effects opt in.
    public readonly struct AbilityTargetBudget {
        public readonly int AllActors, Hostiles;
        public AbilityTargetBudget(int allActors, int hostiles) { this.AllActors = allActors; this.Hostiles = hostiles; }
    }
}

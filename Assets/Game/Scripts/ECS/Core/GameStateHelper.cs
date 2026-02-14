using Game.ECS.Components;
using Scellecs.Morpeh;
using Game.Domain;

namespace Game.ECS.Core {
    internal static class GameStateHelper {
        public static bool IsCombat(EntityLookup entities, Stash<GameStateComponent> stash) {
            return entities.TryGetGameState(out var entity) &&
                   stash.Has(entity) && stash.Get(entity).phase == SessionPhase.Combat;
        }
        public static bool IsGameOver(EntityLookup entities, Stash<GameStateComponent> gameStateStash) {
            return entities.TryGetGameState(out var stateEntity) &&
                   gameStateStash.Has(stateEntity) &&
                   gameStateStash.Get(stateEntity).isGameOver;
        }
    }
}

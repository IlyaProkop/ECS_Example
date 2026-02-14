using Scellecs.Morpeh;

namespace Game.ECS.Core {
    internal sealed class EntityLookup {
        public Entity player;
        public Entity gameState;

        public bool TryGetPlayer(out Entity entity) {
            return TryGetValid(this.player, out entity);
        }

        public bool TryGetGameState(out Entity entity) {
            return TryGetValid(this.gameState, out entity);
        }

        private static bool TryGetValid(Entity source, out Entity entity) {
            if (source != null && !source.IsNullOrDisposed()) {
                entity = source;
                return true;
            }

            entity = null;
            return false;
        }
    }
}

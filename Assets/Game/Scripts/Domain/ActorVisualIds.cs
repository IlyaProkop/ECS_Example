using System;

namespace Game.Domain {
    // Content IDs cross the simulation boundary; Unity assets remain in presentation.
    public static class ActorVisualIds {
        public const int Player = 1, Enemy = 2, Projectile = 3, Coin = 4;
        public static int DefaultFor(ActorKind kind) => kind switch {
            ActorKind.Player => Player, ActorKind.Enemy => Enemy,
            ActorKind.Projectile => Projectile, ActorKind.Coin => Coin,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}

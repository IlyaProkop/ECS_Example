using Game.ECS.Components;
using Scellecs.Morpeh;
using UnityEngine;
using EntityId = Scellecs.Morpeh.EntityId;

namespace Game.ECS.Spawning {
    internal interface IEntityRecycler {
        // True means this recycler owns the entity, including an already returned entity.
        bool TryRecycle(Entity entity);
    }
    // Each system receives only the creation capability it uses. Spawners borrow the world.
    internal interface IEnemySpawner {
        Entity Spawn(Vector3 position, Game.Domain.EnemyDefinition definition);
    }
    internal interface ICoinSpawner {
        Entity Spawn(Vector3 position);
    }
    internal interface IProjectileSpawner {
        Entity Spawn(in ProjectileSpawn request);
    }

    // A value snapshot: source attribution survives owner changes and structural writes.
    internal readonly struct ProjectileSpawn {
        public readonly Vector3 Position, Direction;
        public readonly float Speed, Damage, Radius, Lifetime;
        public readonly DamageSourceComponent Source;
        public readonly EntityId Target;
        public readonly int DefinitionIndex;
        public readonly int TargetActorId;
        public ProjectileSpawn(Vector3 position, Vector3 direction, float speed, float damage,
            float radius, float lifetime, DamageSourceComponent source, EntityId target = default, int definitionIndex = 0, int targetActorId = 0) {
            this.Position = position;
            this.Direction = direction;
            this.Speed = speed;
            this.Damage = damage;
            this.Radius = radius;
            this.Lifetime = lifetime;
            this.Source = source;
            this.Target = target;
            this.DefinitionIndex = definitionIndex;
            this.TargetActorId = targetActorId;
        }
    }
}

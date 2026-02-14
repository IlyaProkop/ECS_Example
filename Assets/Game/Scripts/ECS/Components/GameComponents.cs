using Scellecs.Morpeh;
using UnityEngine;
using EntityId = Scellecs.Morpeh.EntityId;
using Game.Domain;

namespace Game.ECS.Components {
    internal struct PlayerTag : IComponent {
    }

    internal struct EnemyTag : IComponent {
    }

    internal struct EnemyTypeComponent : IComponent {
        public int id;
    }

    internal struct ProjectileTag : IComponent {
    }

    // Persistent pool membership; generation changes for every borrowed lifetime.
    internal struct ProjectilePoolSlot : IComponent {
        public int index;
        public uint generation;
    }

    internal struct CoinTag : IComponent {
    }

    internal struct DestroyTag : IComponent {
    }

    internal struct PositionComponent : IComponent {
        public Vector3 value;
    }

    internal struct RadiusComponent : IComponent {
        public float value;
    }

    internal struct HealthComponent : IComponent {
        public float current;
        public float max;
    }

    internal struct MoveInputComponent : IComponent {
        public Vector2 value;
    }

    internal struct CombatInputComponent : IComponent {
        public bool attackHeld;
        public bool abilityPressed;
        public uint abilitySlotsPressed;
        public AttackAim aim;
    }

    internal struct AbilitySlot {
        public int definitionId;
        public float cooldown, remainingCooldown;
    }
    internal struct AbilityComponent : IComponent {
        private AbilitySlot first, second, third, fourth;
        public int count;
        public uint requestedSlots;
        public float remainingCooldown => this.first.remainingCooldown;
        public AbilitySlot Get(int slot) => slot switch {
            0 => this.first, 1 => this.second, 2 => this.third, 3 => this.fourth,
            _ => throw new System.ArgumentOutOfRangeException(nameof(slot))
        };
        public void Set(int slot, AbilitySlot value) {
            switch (slot) {
                case 0: this.first = value; break;
                case 1: this.second = value; break;
                case 2: this.third = value; break;
                case 3: this.fourth = value; break;
                default: throw new System.ArgumentOutOfRangeException(nameof(slot));
            }
        }
    }

    internal struct DefenseComponent : IComponent {
        public float armor;
    }

    // Slot in the session-owned stat tables. Speed/Defense are derived hot-path mirrors.
    internal struct StatOwnerComponent : IComponent {
        public int slot;
    }

    // Direct attribution, independent of transform hierarchy or projectile lifetime.
    internal struct DamageSourceComponent : IComponent {
        public EntityId owner;
        public Team team;
        public float criticalChance;
        public float criticalMultiplier;
    }

    internal struct MoveSpeedComponent : IComponent {
        public float value;
    }

    internal struct DamageMultiplierComponent : IComponent {
        public float value;
    }

    internal struct ResourceComponent : IComponent {
        public int coins;
    }

    internal struct WeaponComponent : IComponent {
        public int definitionIndex;
        public float cooldown;
    }
    internal struct AttackIntentComponent : IComponent {
        public bool held;
        public AttackAim aim;
    }

    internal struct EnemyBehaviourComponent : IComponent {
        public float stopDistance;
        public float attackRange;
        public float contactDamagePerSecond;
    }

    // Only the chase producer knows about the player. Other behaviours own the same goal.
    internal struct ChasePlayerTag : IComponent { }
    internal struct MovementGoalComponent : IComponent {
        public Vector3 position;
        public float stoppingDistance;
        public bool active;
    }

    internal enum PathStatus : byte { None, Direct, Route, Blocked }

    internal struct EnemyPathComponent : IComponent {
        public int slot;
        public PathStatus status;
        public int waypointCount;
        public int currentWaypointIndex;
        public float repathTimer;
        public Vector3 lastTargetPosition;
    }

    internal struct EnemyVelocityComponent : IComponent {
        public Vector3 value;
    }

    internal struct ProjectileComponent : IComponent {
        public Vector3 previousPosition;
        public Vector3 direction;
        public float speed;
        public float damage;
        public float radius;
        public float remainingLifetime;
    }

    internal struct ProjectilePayloadComponent : IComponent {
        public int definitionIndex;
        public EntityId target;
        public int targetActorId;
    }

    internal struct CoinComponent : IComponent {
        public float pickupRadius;
    }

    internal struct GameStateComponent : IComponent {
        public SessionPhase phase;
        public BattleOutcome outcome;
        public int spawnedEnemies;
        public float spawnTimer;
        public float resultsTimer;
        public bool isGameOver => this.phase == SessionPhase.Results || this.phase == SessionPhase.Meta;
    }

    internal struct BattleStatisticsComponent : IComponent {
        public BattleStatistics value;
    }

    internal struct ActorComponent : IComponent {
        public int visualId;
        public int id;
        public ActorKind kind;
    }
}

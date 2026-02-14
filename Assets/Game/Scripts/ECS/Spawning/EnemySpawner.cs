using System;
using Game.Domain;
using Game.ECS.Components;
using Scellecs.Morpeh;
using UnityEngine;
using Random = System.Random;

namespace Game.ECS.Spawning {
    internal sealed class EnemySpawner : IEnemySpawner {
        private readonly ActorFactory actors;
        private readonly SimulationSettings config;
        private readonly Random navigationRandom;
        private readonly Stash<EnemyTag> enemies;
        private readonly Stash<DamageSourceComponent> sources;
        private readonly Stash<EnemyBehaviourComponent> behaviours;
        private readonly Stash<EnemyPathComponent> paths;
        private readonly Stash<EnemyVelocityComponent> velocities;
        private readonly Stash<EnemyTypeComponent> types;
        private int nextPathSlot;
        private readonly World world;
        private readonly Weapons.WeaponCatalog weapons;

        public EnemySpawner(World world, ActorFactory actors, SimulationSettings config, Random navigationRandom, Weapons.WeaponCatalog weapons) {
            this.world = world;
            this.weapons = weapons;
            this.actors = actors;
            this.config = config;
            this.navigationRandom = navigationRandom;
            this.enemies = world.GetStash<EnemyTag>();
            this.sources = world.GetStash<DamageSourceComponent>();
            this.behaviours = world.GetStash<EnemyBehaviourComponent>();
            this.paths = world.GetStash<EnemyPathComponent>();
            this.velocities = world.GetStash<EnemyVelocityComponent>();
            this.types = world.GetStash<EnemyTypeComponent>();
        }

        public Entity Spawn(Vector3 position, EnemyDefinition definition) {
            // Validate the finite-wave budget before consuming an ID, slot or random sample.
            if (this.nextPathSlot >= this.config.enemyCount) throw new InvalidOperationException("Session enemy capacity exhausted.");
            if (definition == null || !this.config.EnemyTypes.Contains(definition))
                throw new ArgumentException("Spawn an enemy definition owned by this session.", nameof(definition));
            var entity = this.actors.Create(ActorKind.Enemy, new Vector3(position.x, 0f, position.z), definition.Radius, definition.VisualId);
            this.actors.SetVitals(entity, definition.MaxHealth, definition.Armor, definition.MoveSpeed);
            this.enemies.Add(entity);
            this.types.Set(entity, new EnemyTypeComponent { id = definition.Id });
            this.sources.Set(entity, new DamageSourceComponent { owner = entity.ID, team = Team.Enemy, criticalMultiplier = 1f });
            this.behaviours.Set(entity, new EnemyBehaviourComponent {
                stopDistance = definition.StopDistance, attackRange = definition.AttackRange,
                contactDamagePerSecond = definition.DamagePerSecond
            });
            var interval = this.config.enemyPathRepathInterval;
            this.paths.Set(entity, new EnemyPathComponent {
                slot = this.nextPathSlot++,
                repathTimer = interval > 0f ? (float)this.navigationRandom.NextDouble() * interval : 0f,
                lastTargetPosition = position
            });
            Abilities.AbilityEquipment.Initialize(this.world, entity, definition.Abilities);
            this.velocities.Add(entity);
            this.world.GetStash<ChasePlayerTag>().Add(entity);
            this.world.GetStash<MovementGoalComponent>().Set(entity, new MovementGoalComponent {
                position = position, stoppingDistance = definition.StopDistance, active = false
            });
            if (definition.Weapon != null) Weapons.WeaponEquipment.Initialize(this.world, entity, this.weapons, definition.Weapon.Id);
            return entity;
        }
    }
}

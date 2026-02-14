using Game.Domain;
using Game.ECS.Components;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Spawning {
    internal sealed class PlayerSpawner {
        private readonly ActorFactory actors;
        private readonly SimulationSettings config;
        private readonly Stash<PlayerTag> players;
        private readonly Stash<CombatInputComponent> inputs;
        private readonly Stash<DamageSourceComponent> sources;
        private readonly Stash<MoveInputComponent> moves;
        private readonly Stash<ResourceComponent> resources;
        private readonly World world;
        private readonly Weapons.WeaponCatalog weapons;

        public PlayerSpawner(World world, ActorFactory actors, SimulationSettings config, Weapons.WeaponCatalog weapons) {
            this.world = world;
            this.weapons = weapons;
            this.actors = actors;
            this.config = config;
            this.players = world.GetStash<PlayerTag>();
            this.inputs = world.GetStash<CombatInputComponent>();
            this.sources = world.GetStash<DamageSourceComponent>();
            this.moves = world.GetStash<MoveInputComponent>();
            this.resources = world.GetStash<ResourceComponent>();
        }

        public Entity Spawn(Vector3 position) {
            var entity = this.actors.Create(ActorKind.Player, position, this.config.playerRadius);
            this.actors.SetVitals(entity, this.config.playerMaxHealth, this.config.playerArmor, this.config.playerMoveSpeed);
            this.players.Add(entity);
            this.inputs.Add(entity);
            Abilities.AbilityEquipment.Initialize(this.world, entity, this.config.PlayerAbilities);
            this.sources.Set(entity, new DamageSourceComponent {
                owner = entity.ID, team = Team.Player,
                criticalChance = this.config.criticalChance, criticalMultiplier = this.config.criticalMultiplier
            });
            this.moves.Add(entity);
            this.resources.Add(entity);
            Weapons.WeaponEquipment.Initialize(this.world, entity, this.weapons, this.config.PlayerWeapon.Id);
            return entity;
        }
    }
}

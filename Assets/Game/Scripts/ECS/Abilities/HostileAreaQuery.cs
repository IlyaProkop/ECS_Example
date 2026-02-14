using Game.Domain;
using Game.ECS.Components;
using Game.Spatial;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Abilities {
    internal interface IAreaTargetVisitor { void Visit(Entity target); }
    // Current team topology: player -> indexed enemies; enemy -> player filter only.
    // A cast by an enemy never scans the enemy population.
    internal sealed class HostileAreaQuery {
        private readonly EnemySpatialIndex enemies;
        private readonly ObstacleGridLookup obstacles;
        private readonly Filter players;
        private readonly Stash<PositionComponent> positions;
        private readonly Stash<HealthComponent> health;
        public HostileAreaQuery(World world, EnemySpatialIndex enemies, ObstacleGridLookup obstacles) {
            this.enemies = enemies; this.obstacles = obstacles;
            this.players = world.Filter.With<PlayerTag>().With<PositionComponent>().With<HealthComponent>().Without<DestroyTag>().Build();
            this.positions = world.GetStash<PositionComponent>(); this.health = world.GetStash<HealthComponent>();
        }
        public int Visit<T>(Team source, Vector3 center, float radius, ref T visitor) where T : struct, IAreaTargetVisitor {
            var inspected = 0;
            if (source == Team.Player) {
                foreach (var sample in this.enemies.QueryCircle(center, radius)) {
                    inspected++;
                    if (sample.IsAlive && this.Visible(center, this.positions.Get(sample.Entity).value)) visitor.Visit(sample.Entity);
                }
            } else if (source == Team.Enemy) {
                foreach (var player in this.players) {
                    inspected++;
                    var point = this.positions.Get(player).value;
                    var delta = point - center; delta.y = 0f;
                    if (this.health.Get(player).current > 0f && delta.sqrMagnitude <= radius * radius && this.Visible(center, point)) visitor.Visit(player);
                }
            }
            return inspected;
        }
        private bool Visible(Vector3 from, Vector3 to) => ObstacleQueries.HasLineOfSight(from, to, 0f, this.obstacles.AsNative());
    }
}

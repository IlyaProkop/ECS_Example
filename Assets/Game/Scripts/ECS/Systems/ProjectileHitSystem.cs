using Game.ECS.Components;
using Game.ECS.Core;
using Game.Spatial;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Systems {
    internal sealed class ProjectileHitSystem : ISystem {
        private readonly EntityLookup entities;
        private readonly Game.Spatial.EnemySpatialIndex spatial;
        private readonly Game.Spatial.ObstacleGridLookup obstacles;
        private readonly Projectiles.ProjectileRuntimeCatalog catalog;

        private Filter projectileFilter;

        private Stash<PositionComponent> positionStash;
        private Stash<ProjectileComponent> projectileStash;
        private Stash<HealthComponent> healthStash;
        private Stash<DestroyTag> destroyStash;
        private Stash<DamageSourceComponent> sources;
        private Stash<GameStateComponent> states;
        private Stash<ProjectilePayloadComponent> payloads;
        private Stash<DamageMultiplierComponent> multipliers;
        private Stash<RadiusComponent> radii;

        public ProjectileHitSystem(EntityLookup entities, Game.Spatial.EnemySpatialIndex spatial, Game.Spatial.ObstacleGridLookup obstacles, Projectiles.ProjectileRuntimeCatalog catalog) {
            this.entities = entities;
            this.spatial = spatial;
            this.obstacles = obstacles;
            this.catalog = catalog;
        }

        public World World { get; set; }

        public void OnAwake() {
            this.projectileFilter = this.World.Filter
                .With<ProjectileTag>()
                .With<PositionComponent>()
                .With<ProjectileComponent>()
                .With<ProjectilePayloadComponent>()
                .Without<DestroyTag>()
                .Build();

            this.positionStash = this.World.GetStash<PositionComponent>();
            this.projectileStash = this.World.GetStash<ProjectileComponent>();
            this.healthStash = this.World.GetStash<HealthComponent>();
            this.destroyStash = this.World.GetStash<DestroyTag>();
            this.sources = this.World.GetStash<DamageSourceComponent>();
            this.states = this.World.GetStash<GameStateComponent>();
            this.payloads = this.World.GetStash<ProjectilePayloadComponent>();
            this.multipliers = this.World.GetStash<DamageMultiplierComponent>();
            this.radii = this.World.GetStash<RadiusComponent>();
        }

        public void OnUpdate(float deltaTime) {
            if (!GameStateHelper.IsCombat(this.entities, this.states)) return;
            var obstacleLookup = this.obstacles.AsNative();

            foreach (var projectileEntity in this.projectileFilter) {
                var projectile = this.projectileStash.Get(projectileEntity);
                var projectilePosition = this.positionStash.Get(projectileEntity).value;

                var start = projectile.previousPosition;
                var end = projectilePosition;
                var start2D = new Vector2(start.x, start.z);
                var end2D = new Vector2(end.x, end.z);
                var center2D = (start2D + end2D) * 0.5f;
                var halfSegmentLength = Vector2.Distance(start2D, end2D) * 0.5f;
                var queryRadius = halfSegmentLength + projectile.radius + this.spatial.MaxRadius;

                Entity hitEnemy = null;
                var enemyHitT = float.MaxValue;

                var source = this.sources.Get(projectileEntity);
                if (source.team == Game.Domain.Team.Player) foreach (var sample in this.spatial.QueryCircle(new Vector3(center2D.x, 0f, center2D.y), queryRadius)) {
                    var enemy = sample.Entity;
                    if (!sample.IsAlive) {
                        continue;
                    }

                    if (!this.healthStash.Has(enemy)) {
                        continue;
                    }

                    if (this.destroyStash.Has(enemy)) {
                        continue;
                    }

                    var enemyHealth = this.healthStash.Get(enemy);
                    if (enemyHealth.current <= 0f) {
                        continue;
                    }

                    var combinedRadius = projectile.radius + sample.Radius;
                    var enemyPos2D = sample.Position;

                    if (!Geometry2D.SegmentIntersectsCircle(start2D, end2D, enemyPos2D, combinedRadius, out var t)) {
                        continue;
                    }

                    if (t < enemyHitT) {
                        enemyHitT = t;
                        hitEnemy = enemy;
                    }
                }

                if (source.team == Game.Domain.Team.Enemy) {
                    var player = this.entities.player;
                    if (!player.IsNullOrDisposed() && this.healthStash.Get(player).current > 0f && !this.destroyStash.Has(player)) {
                        var position = this.positionStash.Get(player).value;
                        if (Geometry2D.SegmentIntersectsCircle(start2D, end2D, new Vector2(position.x, position.z),
                            projectile.radius + this.radii.Get(player).value, out var t)) { hitEnemy = player; enemyHitT = t; }
                    }
                }

                var hitObstacle = ObstacleQueries.TryFindFirstHit(start2D, end2D, projectile.radius, obstacleLookup, out var obstacleHitT);
                var hitEnemyFirst = hitEnemy != null && (!hitObstacle || enemyHitT < obstacleHitT);

                if (hitEnemyFirst) {
                    var hit = new Game.ECS.Projectiles.ProjectileHit(hitEnemy, Vector3.Lerp(start, end, enemyHitT),
                        projectile.damage * this.multipliers.Get(projectileEntity).value, source);
                    var effects = this.catalog.Get(this.payloads.Get(projectileEntity).definitionIndex).Hits;
                    foreach (var effect in effects) effect.Execute(hit);

                    if (!this.destroyStash.Has(projectileEntity)) {
                        this.destroyStash.Add(projectileEntity);
                    }

                    continue;
                }

                if (hitObstacle) {
                    if (!this.destroyStash.Has(projectileEntity)) {
                        this.destroyStash.Add(projectileEntity);
                    }

                    continue;
                }

                if (projectile.remainingLifetime <= 0f) {
                    if (!this.destroyStash.Has(projectileEntity)) {
                        this.destroyStash.Add(projectileEntity);
                    }
                    continue;
                }
            }
        }

        public void Dispose() {
        }

    }
}

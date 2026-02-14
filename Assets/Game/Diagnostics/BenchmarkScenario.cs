using System;
using Game.Config;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Diagnostics {
    // Owns temporary authoring assets; gameplay definitions and production balance stay unchanged.
    internal sealed class BenchmarkScenario : IDisposable {
        public readonly string Name;
        public bool Stationary => this.Name != "route";
        public int SettleFrames => this.Stationary ? 3600 : 0;
        public Vector3 Target => this.Name == "choke" ? new Vector3(0f, 0f, 6f) : Vector3.zero;
        private WeaponAsset weapon;
        private ProjectileAsset projectile;
        private DirectDamageHitAsset hit;

        public BenchmarkScenario(string name) {
            if (name != "route" && name != "crowd" && name != "choke" && name != "volley")
                throw new ArgumentException("Scenario must be route, crowd, choke or volley.");
            this.Name = name;
        }
        public void Configure(GameConfig config) {
            if (!this.Stationary) return;
            config.playerMoveSpeed = 30f; // Reach the fixed test position during population warmup.
            config.playerMaxHealth = 100000000f;
            config.obstaclePositions = Array.Empty<Vector3>();
            if (this.Name == "choke") {
                const float gapHalfWidth = 2f;
                var wallWidth = config.arenaHalfSize.x - gapHalfWidth;
                var center = gapHalfWidth + wallWidth * 0.5f;
                config.obstacleScale = new Vector3(wallWidth, 2f, 4f);
                config.obstaclePositions = new[] { new Vector3(-center, 0f, 0f), new Vector3(center, 0f, 0f) };
            }
            if (this.Name != "volley") return;
            if (config.enemyCount > 2000) throw new ArgumentException("Volley supports at most 2000 enemies within the existing projectile budget.");
            this.hit = ScriptableObject.CreateInstance<DirectDamageHitAsset>();
            this.projectile = ScriptableObject.CreateInstance<ProjectileAsset>();
            this.projectile.speed = 80f; this.projectile.damage = 1f; this.projectile.lifetime = 1f;
            this.projectile.hitEffects = new ProjectileHitAsset[] { this.hit };
            this.weapon = ScriptableObject.CreateInstance<WeaponAsset>();
            this.weapon.id = 9001; this.weapon.interval = 0.5f; this.weapon.range = 1000f;
            this.weapon.projectile = this.projectile;
            config.playerWeapon = this.weapon;
            config.availableWeapons = Array.Empty<WeaponAsset>();
            foreach (var enemy in config.enemyTypes) enemy.weapon = this.weapon;
        }
        public void Dispose() { Object.Destroy(this.weapon); Object.Destroy(this.projectile); Object.Destroy(this.hit); }
    }
}

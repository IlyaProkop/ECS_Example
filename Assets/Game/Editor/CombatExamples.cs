using System;
using Game.Config;
using Game.Domain.Stats;
using UnityEditor;
using UnityEngine;

namespace Game.Editor {
    public static class CombatExamples {
        private const string Directory = "Assets/Game/Examples/Combat";
        [MenuItem("Game/Create combat examples")]
        public static void Create() {
            if (!AssetDatabase.IsValidFolder("Assets/Game/Examples")) AssetDatabase.CreateFolder("Assets/Game", "Examples");
            if (!AssetDatabase.IsValidFolder(Directory)) AssetDatabase.CreateFolder("Assets/Game/Examples", "Combat");
            var damage = Asset<DirectDamageHitAsset>("DirectDamage", _ => { });
            var burn = Asset<StatusEffectAsset>("Burn", a => { a.id = 10; a.duration = 3f; a.periodicDamage = true; a.tickInterval = .5f; a.tickDamage = 5f; });
            var slow = Asset<StatusEffectAsset>("Slow", a => { a.id = 11; a.duration = 2f;
                a.modifiers = new[] { new StatModifierAuthoring { statId = StatIds.MoveSpeed, operation = StatOperation.Multiply, value = .5f } }; });
            var power = Asset<StatusEffectAsset>("DoubleDamage", a => { a.id = 12; a.duration = 3f;
                a.modifiers = new[] { new StatModifierAuthoring { statId = StatIds.DamageMultiplier, operation = StatOperation.Multiply, value = 2f } }; });
            var fireHit = Asset<StatusHitAsset>("IgniteOnHit", a => a.status = burn);
            var slowHit = Asset<StatusHitAsset>("SlowOnHit", a => a.status = slow);
            var straight = Projectile("Straight", ProjectileMotionMode.Straight, new ProjectileHitAsset[] { damage });
            var homing = Projectile("Homing", ProjectileMotionMode.Homing, new ProjectileHitAsset[] { damage });
            var fire = Projectile("Incendiary", ProjectileMotionMode.Straight, new ProjectileHitAsset[] { damage, fireHit });
            Projectile("HomingIncendiary", ProjectileMotionMode.Homing, new ProjectileHitAsset[] { damage, fireHit });
            Projectile("Slowing", ProjectileMotionMode.Straight, new ProjectileHitAsset[] { damage, slowHit });
            Asset<WeaponAsset>("StraightWeapon", a => { a.id = 1; a.projectile = straight; });
            var homingWeapon = Asset<WeaponAsset>("HomingWeapon", a => { a.id = 2; a.projectile = homing; });
            var fireWeapon = Asset<WeaponAsset>("IncendiaryWeapon", a => { a.id = 3; a.projectile = fire; });
            Asset<GameConfig>("SharedWeaponsConfig", a => {
                a.enemyCount = 2; a.playerWeapon = fireWeapon; a.availableWeapons = new[] { homingWeapon };
                a.enemyTypes = new[] { new EnemyAuthoring { weapon = fireWeapon,
                    stopDistance = 6f, attackRange = 6f, damagePerSecond = 0f } };
            });
            Asset<AbilityAsset>("EmpowerAbility", a => { a.id = 20; a.displayName = "Усиление";
                a.effects = new AbilityEffectAuthoring[] { new SelfStatusAuthoring { status = power } }; });
            Asset<AbilityAsset>("SlowAreaAbility", a => { a.id = 21; a.displayName = "Замедление";
                a.effects = new AbilityEffectAuthoring[] { new AreaStatusAuthoring { radius = 5f, targets = StatusTargets.Enemy | StatusTargets.Projectile, status = slow } }; });
            AssetDatabase.SaveAssets();
            Debug.Log("Combat examples ready at " + Directory + ". Assign a weapon/ability in GameConfig.");
        }
        private static ProjectileAsset Projectile(string name, ProjectileMotionMode motion, ProjectileHitAsset[] hits) =>
            Asset<ProjectileAsset>(name, a => { a.motion = motion; a.hitEffects = hits; });
        private static T Asset<T>(string name, Action<T> initialize) where T : ScriptableObject {
            var path = Directory + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>(); initialize(asset); AssetDatabase.CreateAsset(asset, path); return asset;
        }
    }
}

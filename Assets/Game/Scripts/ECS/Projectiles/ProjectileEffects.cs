using System;
using System.Collections.Generic;
using Game.Domain;
using Game.ECS.Components;
using Game.ECS.Core;
using Game.ECS.Stats;
using Scellecs.Morpeh;
using UnityEngine;

namespace Game.ECS.Projectiles {
    internal readonly struct ProjectileHit {
        public readonly Entity Target;
        public readonly Vector3 Position;
        public readonly float Damage;
        public readonly DamageSourceComponent Source;
        public ProjectileHit(Entity target, Vector3 position, float damage, DamageSourceComponent source) {
            this.Target = target; this.Position = position; this.Damage = damage; this.Source = source;
        }
    }
    internal interface IProjectileHitEffect : IDisposable { void Execute(in ProjectileHit hit); }
    internal sealed class ProjectileDamageEffect : IProjectileHitEffect {
        private readonly FrameBuffer<DamageRequest> requests;
        private readonly DamageKind kind;
        public ProjectileDamageEffect(DirectProjectileDamage definition, FrameBuffer<DamageRequest> requests) {
            this.requests = requests; this.kind = definition.Kind;
        }
        public void Execute(in ProjectileHit hit) => this.requests.Add(new DamageRequest {
            target = hit.Target, source = hit.Source, value = hit.Damage, kind = this.kind
        });
        public void Dispose() { }
    }
    internal sealed class ProjectileStatusEffect : IProjectileHitEffect {
        private readonly StatStorage stats;
        private readonly CompiledStatus status;
        public ProjectileStatusEffect(ProjectileStatusHit definition, StatStorage stats) {
            this.stats = stats; this.status = stats.Compile(definition.Status);
        }
        public void Execute(in ProjectileHit hit) {
            if (this.stats.CanReceive(hit.Target)) this.stats.TryApply(hit.Target,
                new EffectSource(hit.Source.owner, this.status.Definition.Id), this.status, hit.Source, out _);
        }
        public void Dispose() { }
    }
    internal sealed class CompiledProjectile : IDisposable {
        public readonly ProjectileDefinition Definition;
        public readonly IProjectileHitEffect[] Hits;
        public readonly CompiledStatus[] SpawnStatuses;
        public CompiledProjectile(ProjectileDefinition definition, IProjectileHitEffect[] hits, CompiledStatus[] statuses) {
            this.Definition = definition; this.Hits = hits; this.SpawnStatuses = statuses;
        }
        public void Dispose() { foreach (var hit in this.Hits) hit.Dispose(); }
    }
    internal sealed class ProjectileRuntimeCatalog : IDisposable {
        private readonly Dictionary<ProjectileDefinition, int> indices = new();
        private readonly List<CompiledProjectile> compiled = new();
        private readonly StatStorage stats;
        private readonly EffectRegistry<ProjectileHitDefinition, IProjectileHitEffect> registry;
        public ProjectileRuntimeCatalog(StatStorage stats, EffectRegistry<ProjectileHitDefinition, IProjectileHitEffect> registry) {
            this.stats = stats;
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }
        public int Compile(ProjectileDefinition definition) {
            if (this.indices.TryGetValue(definition, out var existing)) return existing;
            var statuses = new CompiledStatus[definition.SpawnStatuses.Count];
            for (var i = 0; i < statuses.Length; i++) statuses[i] = this.stats.Compile(definition.SpawnStatuses[i]);
            var result = new CompiledProjectile(definition, this.registry.Compile(definition.Hits), statuses);
            var index = this.compiled.Count;
            this.compiled.Add(result); this.indices.Add(definition, index);
            return index;
        }
        public CompiledProjectile Get(int index) => this.compiled[index];
        public void Dispose() {
            foreach (var projectile in this.compiled) projectile.Dispose();
            this.compiled.Clear(); this.indices.Clear();
        }
    }
}

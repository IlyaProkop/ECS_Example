using System;
using System.Collections.Generic;
using Game.ECS.Components;
using Game.ECS.Stats;
using Scellecs.Morpeh;

namespace Game.ECS.Spawning {
    // Entity/EntityId identify storage, not a pooled shot. Delayed users keep this lease.
    internal readonly struct ProjectileLease {
        internal readonly Entity Entity;
        internal readonly uint Generation;
        internal ProjectileLease(Entity entity, uint generation) { this.Entity = entity; this.Generation = generation; }
    }

    // Fixed session budget. Preparation can yield between batches; Rent never grows the pool.
    internal sealed class ProjectilePool : IEntityRecycler, IDisposable {
        private readonly Entity[] entities;
        private readonly int[] free;
        private readonly bool[] rented;
        private readonly StatStorage stats;
        private readonly List<Stash> extensionComponents = new();
        private World world;
        private Stash<ProjectilePoolSlot> slots;
        private ProjectilePoolData data;
        private int created, prepared, freeCount;
        private bool disposed;
        public int Capacity => this.entities.Length;
        public int ActiveCount { get; private set; }
        public int PeakActiveCount { get; private set; }
        public int CreatedCount => this.created;
        public bool IsPrepared => this.prepared == this.Capacity;
        public ProjectilePool(int capacity, StatStorage stats) {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.stats = stats ?? throw new ArgumentNullException(nameof(stats));
            this.entities = new Entity[capacity]; this.free = new int[capacity]; this.rented = new bool[capacity];
        }
        public void Attach(World world) {
            this.CheckAlive();
            if (this.world != null) {
                if (!ReferenceEquals(this.world, world)) throw new InvalidOperationException("Projectile pool belongs to another world.");
                return;
            }
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.slots = world.GetStash<ProjectilePoolSlot>(); this.data = new ProjectilePoolData(world);
            this.stats.Attach(world);
        }
        // A spawner extension declares any additional component that must not survive return.
        public void RegisterReset<T>() where T : struct, IComponent {
            this.CheckAlive();
            if (this.world == null || this.created != 0) throw new InvalidOperationException("Register resets after Attach and before preparation.");
            if (typeof(T) == typeof(ProjectilePoolSlot)) throw new ArgumentException("Pool membership cannot be reset by an extension.");
            var stash = this.world.GetStash<T>();
            if (!this.extensionComponents.Contains(stash)) this.extensionComponents.Add(stash);
        }
        public bool PrepareBatch(int budget = 64) {
            this.CheckAlive();
            if (this.world == null || budget < 1) throw new InvalidOperationException("Attach a world and supply a positive preparation budget.");
            for (var i = 0; i < budget && this.created < this.Capacity; i++) {
                var index = this.created++; var entity = this.world.CreateEntity(); this.entities[index] = entity;
                this.slots.Set(entity, new ProjectilePoolSlot { index = index });
                this.data.Prepare(entity);
            }
            // Warm membership stashes with the full capacity before removing their entries.
            if (this.created == this.Capacity) for (var i = 0; i < budget && this.prepared < this.Capacity; i++) {
                var index = this.prepared++; this.data.Reset(this.entities[index]); this.free[this.freeCount++] = index;
            }
            return this.IsPrepared;
        }
        public Entity Rent() {
            this.CheckAlive();
            if (!this.IsPrepared) throw new InvalidOperationException("Complete projectile preparation before spawning.");
            if (this.freeCount == 0) throw new InvalidOperationException("Concurrent projectile capacity exhausted.");
            var index = this.free[this.freeCount - 1]; var entity = this.entities[index];
            if (entity.IsNullOrDisposed()) throw new InvalidOperationException("A pooled projectile was removed outside its recycler.");
            var slot = this.slots.Get(entity); slot.generation = checked(slot.generation + 1);
            this.freeCount--; this.rented[index] = true; this.slots.Set(entity, slot);
            this.ActiveCount++; this.PeakActiveCount = Math.Max(this.PeakActiveCount, this.ActiveCount);
            return entity;
        }
        public ProjectileLease Capture(Entity entity) {
            this.CheckAlive();
            if (!this.Find(entity, out var slot) || !this.rented[slot.index]) throw new ArgumentException("Projectile is not rented from this pool.");
            return new ProjectileLease(entity, slot.generation);
        }
        public bool TryResolve(in ProjectileLease lease, out Entity entity) {
            this.CheckAlive(); entity = null;
            if (!this.Find(lease.Entity, out var slot) || slot.generation != lease.Generation || !this.rented[slot.index]) return false;
            entity = lease.Entity; return true;
        }
        public bool TryReturn(in ProjectileLease lease) {
            if (!this.TryResolve(lease, out var entity)) return false;
            this.Return(entity, this.slots.Get(entity).index); return true;
        }
        public bool TryRecycle(Entity entity) {
            this.CheckAlive();
            if (!this.Find(entity, out var slot)) return false;
            if (this.rented[slot.index]) this.Return(entity, slot.index);
            return true;
        }
        private bool Find(Entity entity, out ProjectilePoolSlot slot) {
            slot = default;
            if (entity.IsNullOrDisposed() || this.world == null || !this.world.TryGetEntity(entity.ID, out var own) ||
                !ReferenceEquals(own, entity) || !this.slots.Has(entity)) return false;
            slot = this.slots.Get(entity);
            return (uint)slot.index < this.entities.Length && ReferenceEquals(this.entities[slot.index], entity);
        }
        private void Return(Entity entity, int index) {
            this.stats.Release(entity);
            this.data.Reset(entity);
            foreach (var stash in this.extensionComponents) stash.Remove(entity);
            this.rented[index] = false; this.free[this.freeCount++] = index; this.ActiveCount--;
        }
        private void CheckAlive() { if (this.disposed) throw new ObjectDisposedException(nameof(ProjectilePool)); }
        public void Dispose() {
            if (this.disposed) return;
            this.disposed = true;
            // World owns Entity destruction; this owner only releases borrowed references.
            Array.Clear(this.entities, 0, this.entities.Length); Array.Clear(this.rented, 0, this.rented.Length);
            this.extensionComponents.Clear(); this.world = null; this.slots = null; this.data = null;
            this.ActiveCount = this.freeCount = 0;
        }
    }
}

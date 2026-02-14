using System;
using Game.ECS.Components;
using Scellecs.Morpeh;

namespace Game.ECS.Stats {
    // ECS ownership and component mirrors only. Row allocation, status lifetimes and
    // stat calculations remain in StatStorage; no callbacks or per-step allocations.
    internal sealed class StatOwnerBindings : IDisposable {
        private readonly Entity[] owners;
        private readonly EntityId[] ownerIds;
        private World world;
        private Stash<StatOwnerComponent> slots;
        private Stash<MoveSpeedComponent> speeds;
        private Stash<DefenseComponent> defenses;
        private Stash<HealthComponent> health;
        private Stash<DestroyTag> destroyed;
        private Stash<DamageMultiplierComponent> damageMultipliers;
        public int Capacity => this.owners.Length;
        public StatOwnerBindings(int capacity) {
            this.owners = new Entity[capacity];
            this.ownerIds = new EntityId[capacity];
        }
        public void Attach(World world) {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (this.world != null) {
                if (!ReferenceEquals(this.world, world)) throw new InvalidOperationException("Stats belong to another world.");
                return;
            }
            this.world = world;
            this.slots = world.GetStash<StatOwnerComponent>();
            this.speeds = world.GetStash<MoveSpeedComponent>();
            this.defenses = world.GetStash<DefenseComponent>();
            this.health = world.GetStash<HealthComponent>();
            this.destroyed = world.GetStash<DestroyTag>();
            this.damageMultipliers = world.GetStash<DamageMultiplierComponent>();
        }
        public void ValidateRegistration(Entity entity) {
            if (this.world == null || entity.IsNullOrDisposed() ||
                !this.world.TryGetEntity(entity.ID, out var ownEntity) || !ReferenceEquals(ownEntity, entity) || this.slots.Has(entity))
                throw new InvalidOperationException("Invalid or already registered stat owner.");
        }
        public void Assign(int row, Entity entity) { this.owners[row] = entity; this.ownerIds[row] = entity.ID; }
        public void PublishSlot(Entity entity, int row) => this.slots.Set(entity, new StatOwnerComponent { slot = row });
        public void PublishValues(int row, float speed, float armor, float damage) {
            var entity = this.owners[row];
            this.speeds.Set(entity, new MoveSpeedComponent { value = speed });
            this.defenses.Set(entity, new DefenseComponent { armor = armor });
            this.damageMultipliers.Set(entity, new DamageMultiplierComponent { value = damage });
        }
        public Entity OwnerAt(int row) => this.owners[row];
        public bool IsAllocated(int row) => this.owners[row] != null;
        public bool HasSlot(Entity target) => !target.IsNullOrDisposed() && this.slots.Has(target);
        public bool IsRegistered(Entity target) => !target.IsNullOrDisposed() && this.world != null &&
            this.world.TryGetEntity(target.ID, out var entity) && ReferenceEquals(entity, target) && this.slots.Has(target);
        public void RemoveSlot(Entity target) => this.slots.Remove(target);
        public void ClearRow(int row) { this.owners[row] = null; this.ownerIds[row] = default; }
        public int GetRow(Entity target, int rowCount) {
            if (target.IsNullOrDisposed() || this.slots == null ||
                !this.world.TryGetEntity(target.ID, out var ownEntity) || !ReferenceEquals(ownEntity, target) || !this.slots.Has(target))
                throw new ArgumentException("Target has no runtime stats in this world.");
            var row = this.slots.Get(target).slot;
            if (row < 0 || row >= rowCount || !ReferenceEquals(this.owners[row], target) || !this.IsValid(row)) throw new ArgumentException("Stale or foreign stat owner.");
            return row;
        }
        public bool IsValid(int row) => !this.owners[row].IsNullOrDisposed() && this.owners[row].ID == this.ownerIds[row];
        public bool IsLiving(int row) => this.IsValid(row) && !this.destroyed.Has(this.owners[row]) &&
            (!this.health.Has(this.owners[row]) || this.health.Get(this.owners[row]).current > 0f);
        public void Dispose() {
            Array.Clear(this.owners, 0, this.owners.Length);
            Array.Clear(this.ownerIds, 0, this.ownerIds.Length);
            this.world = null; this.slots = null; this.speeds = null; this.defenses = null;
            this.health = null; this.destroyed = null; this.damageMultipliers = null;
        }
    }
}

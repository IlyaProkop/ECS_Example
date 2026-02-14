using System;
using System.Collections.Generic;
using System.Threading;
using Game.Domain.Stats;
using Game.ECS.Components;
using Scellecs.Morpeh;

namespace Game.ECS.Stats {
    internal readonly struct EffectSource : IEquatable<EffectSource> {
        public readonly EntityId Owner;
        public readonly int Key;
        public EffectSource(EntityId owner, int key) { this.Owner = owner; this.Key = key; }
        public bool Equals(EffectSource other) => this.Owner == other.Owner && this.Key == other.Key;
    }
    internal readonly struct StatusHandle {
        internal readonly long StorageId, Sequence;
        internal readonly int Index;
        internal StatusHandle(long storageId, long sequence, int index) {
            this.StorageId = storageId; this.Sequence = sequence; this.Index = index;
        }
    }
    internal sealed class CompiledStatus {
        internal readonly StatStorage Owner;
        public readonly StatusEffectDefinition Definition;
        private readonly int[] statIndices;
        internal CompiledStatus(StatStorage owner, StatusEffectDefinition definition, StatCatalog catalog) {
            this.Owner = owner; this.Definition = definition;
            this.statIndices = new int[definition.Modifiers.Count];
            for (var i = 0; i < this.statIndices.Length; i++) this.statIndices[i] = catalog.IndexOf(definition.Modifiers[i].StatId);
        }
        public int StatIndex(int modifier) => this.statIndices[modifier];
    }

    // Session-owned flat tables. Gameplay reads compact mirrors, not these tables or definitions.
    internal sealed class StatStorage : IDisposable {
        public const int StatusCapacityPerActor = 16;
        private static long nextStorageId;
        private readonly long storageId = Interlocked.Increment(ref nextStorageId);
        private readonly StatCatalog catalog;
        private readonly int statCount, speedIndex, armorIndex, damageIndex;
        private readonly int[] freeRows;
        private int freeCount;
        private readonly IStatusDamageSink damageSink;
        private readonly StatOwnerBindings bindings;
        private readonly float[] bases, finals;
        private readonly bool[] dirty;
        private readonly int[] dirtyRows, activeIndices;
        private readonly Instance[] instances;
        private readonly Dictionary<int, CompiledStatus> definitions = new Dictionary<int, CompiledStatus>();
        private int rowCount, dirtyCount, activeCount;
        private long sequence;
        private double time;
        private bool disposed;
        public int RecalculationCount { get; private set; }
        public int ActiveCount => this.activeCount;

        private struct Instance {
            public CompiledStatus Status;
            public EffectSource Source;
            public double ExpiresAt;
            public double NextTick;
            public DamageSourceComponent Attribution;
            public long Sequence;
            public int ActiveIndex;
        }
        public StatStorage(StatCatalog catalog, int actorCapacity, IStatusDamageSink damageSink = null) {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (actorCapacity < 1) throw new ArgumentOutOfRangeException(nameof(actorCapacity));
            this.statCount = catalog.Definitions.Count;
            this.speedIndex = catalog.IndexOf(StatIds.MoveSpeed);
            this.armorIndex = catalog.IndexOf(StatIds.Armor);
            this.damageIndex = catalog.TryIndexOf(StatIds.DamageMultiplier, out var damage) ? damage : -1;
            this.damageSink = damageSink;
            this.freeRows = new int[actorCapacity];
            this.bindings = new StatOwnerBindings(actorCapacity);
            this.bases = new float[checked(actorCapacity * this.statCount)];
            this.finals = new float[this.bases.Length];
            this.dirty = new bool[actorCapacity];
            this.dirtyRows = new int[actorCapacity];
            this.instances = new Instance[checked(actorCapacity * StatusCapacityPerActor)];
            this.activeIndices = new int[this.instances.Length];
        }
        public void Attach(World world) { this.CheckAlive(); this.bindings.Attach(world); }
        public void Register(Entity entity, float speed, float armor) {
            this.CheckAlive();
            Finite(speed); Finite(armor);
            this.bindings.ValidateRegistration(entity);
            this.EnsureCapacity();
            var row = this.freeCount > 0 ? this.freeRows[--this.freeCount] : this.rowCount++;
            this.bindings.Assign(row, entity);
            var offset = row * this.statCount;
            for (var i = 0; i < this.statCount; i++) this.bases[offset + i] = this.catalog.Definitions[i].DefaultValue;
            this.bases[offset + this.speedIndex] = speed;
            this.bases[offset + this.armorIndex] = armor;
            this.bindings.PublishSlot(entity, row);
            this.MarkDirty(row);
            // Initial mirrors must exist before the first movement/damage system sees the actor.
            this.RecalculateDirty();
        }
        public CompiledStatus Compile(StatusEffectDefinition definition) {
            this.CheckAlive();
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (this.definitions.TryGetValue(definition.Id, out var existing)) {
                var old = existing.Definition;
                var a = old.PeriodicDamage; var b = definition.PeriodicDamage;
                if ((a == null) != (b == null) || (a != null && (a.Damage != b.Damage || a.Interval != b.Interval || a.Kind != b.Kind)))
                    throw new ArgumentException("Conflicting periodic effects for one status ID.");
                if (old.Duration != definition.Duration || old.Stacking != definition.Stacking || old.MaxStacks != definition.MaxStacks || old.Modifiers.Count != definition.Modifiers.Count)
                    throw new ArgumentException("Conflicting definitions for one status ID.");
                for (var i = 0; i < old.Modifiers.Count; i++) if (!old.Modifiers[i].Equals(definition.Modifiers[i]))
                    throw new ArgumentException("Conflicting modifiers for one status ID.");
                return existing;
            }
            var result = new CompiledStatus(this, definition, this.catalog);
            this.definitions.Add(definition.Id, result);
            return result;
        }
        public StatusHandle Apply(Entity target, in EffectSource source, CompiledStatus status) {
            if (!this.TryApply(target, source, status, default, out var handle))
                throw new InvalidOperationException("Target is not eligible or active status capacity exhausted.");
            return handle;
        }
        public bool TryApply(Entity target, in EffectSource source, CompiledStatus status,
            in DamageSourceComponent attribution, out StatusHandle handle) {
            this.CheckAlive();
            handle = default;
            if (status == null || !ReferenceEquals(status.Owner, this)) throw new ArgumentException("Compile the status in this session first.");
            var row = this.GetRow(target);
            if (!this.bindings.IsLiving(row)) return false;
            if (status.Definition.PeriodicDamage != null && this.damageSink == null)
                throw new InvalidOperationException("Periodic damage requires a session damage sink.");
            var start = row * StatusCapacityPerActor;
            var free = -1; var matching = -1; var stackCount = 0;
            for (var i = start; i < start + StatusCapacityPerActor; i++) {
                var instance = this.instances[i];
                if (instance.Status == null) { if (free < 0) free = i; continue; }
                if (instance.Status.Definition.Id == status.Definition.Id && instance.Source.Equals(source)) { matching = i; stackCount++; }
            }
            var refresh = matching >= 0 && status.Definition.Stacking == StatusStacking.Refresh;
            if (!refresh && (stackCount >= status.Definition.MaxStacks || free < 0)) return false;
            var index = refresh ? matching : free;
            var version = checked(this.sequence + 1);
            this.sequence = version;
            var activeIndex = refresh ? this.instances[index].ActiveIndex : this.activeCount++;
            this.activeIndices[activeIndex] = index;
            this.instances[index] = new Instance { Status = status, Source = source, ExpiresAt = this.time + status.Definition.Duration,
                Sequence = version, ActiveIndex = activeIndex, Attribution = attribution,
                NextTick = refresh ? this.instances[index].NextTick : this.time + (status.Definition.PeriodicDamage?.Interval ?? double.PositiveInfinity) };
            this.MarkDirty(row);
            handle = new StatusHandle(this.storageId, version, index);
            return true;
        }
        public bool Remove(StatusHandle handle) {
            this.CheckAlive();
            if (handle.StorageId != this.storageId || handle.Index < 0 || handle.Index >= this.instances.Length ||
                this.instances[handle.Index].Status == null || this.instances[handle.Index].Sequence != handle.Sequence) return false;
            this.RemoveAt(handle.Index);
            return true;
        }
        public int RemoveBySource(Entity target, in EffectSource source) {
            this.CheckAlive();
            var start = this.GetRow(target) * StatusCapacityPerActor;
            var removed = 0;
            for (var i = start; i < start + StatusCapacityPerActor; i++) {
                if (this.instances[i].Status != null && this.instances[i].Source.Equals(source)) { this.RemoveAt(i); removed++; }
            }
            return removed;
        }
        public void SetBase(Entity target, int statId, float value) {
            this.CheckAlive(); Finite(value);
            var row = this.GetRow(target);
            var index = row * this.statCount + this.catalog.IndexOf(statId);
            if (this.bases[index] == value) return;
            this.bases[index] = value;
            this.MarkDirty(row);
        }
        public float GetFinal(Entity target, int statId) => this.finals[this.GetRow(target) * this.statCount + this.catalog.IndexOf(statId)];
        public void Advance(float deltaTime) {
            this.CheckAlive(); Finite(deltaTime);
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            this.time += deltaTime;
            for (var i = this.activeCount - 1; i >= 0; i--) {
                var index = this.activeIndices[i];
                if (!this.bindings.IsLiving(index / StatusCapacityPerActor)) { this.RemoveAt(index); continue; }
                var instance = this.instances[index];
                var periodic = instance.Status.Definition.PeriodicDamage;
                if (periodic != null) {
                    // Include the tick exactly at expiration. Refresh preserves tick phase.
                    while (instance.NextTick <= Math.Min(this.time, instance.ExpiresAt) + 1e-9) {
                        this.damageSink.Tick(this.bindings.OwnerAt(index / StatusCapacityPerActor), instance.Attribution, periodic);
                        instance.NextTick += periodic.Interval;
                    }
                    this.instances[index].NextTick = instance.NextTick;
                }
                if (instance.ExpiresAt <= this.time + 1e-9) this.RemoveAt(index);
            }
        }
        public void ClearInvalidTargets(bool battleEnded) {
            this.CheckAlive();
            for (var i = this.activeCount - 1; i >= 0; i--) {
                var index = this.activeIndices[i];
                if (battleEnded || !this.bindings.IsLiving(index / StatusCapacityPerActor)) this.RemoveAt(index);
            }
        }
        public void RecalculateDirty() {
            this.CheckAlive();
            for (var q = 0; q < this.dirtyCount; q++) {
                var row = this.dirtyRows[q];
                this.dirty[row] = false;
                if (!this.bindings.IsValid(row)) continue;
                var offset = row * this.statCount;
                for (var stat = 0; stat < this.statCount; stat++) {
                    var calculation = new StatCalculation(this.bases[offset + stat]);
                    var start = row * StatusCapacityPerActor;
                    for (var i = start; i < start + StatusCapacityPerActor; i++) {
                        var instance = this.instances[i];
                        if (instance.Status == null) continue;
                        var modifiers = instance.Status.Definition.Modifiers;
                        for (var m = 0; m < modifiers.Count; m++) if (instance.Status.StatIndex(m) == stat)
                            calculation.Apply(modifiers[m], instance.Sequence, m);
                    }
                    this.finals[offset + stat] = calculation.Resolve(this.catalog.Definitions[stat]);
                }
                this.bindings.PublishValues(row, this.finals[offset + this.speedIndex],
                    this.finals[offset + this.armorIndex], this.damageIndex >= 0 ? this.finals[offset + this.damageIndex] : 1f);
                this.RecalculationCount++;
            }
            this.dirtyCount = 0;
        }
        public PlayerStatsSnapshot ReadPlayer(Entity target) {
            var row = this.GetRow(target);
            var start = row * StatusCapacityPerActor;
            var count = 0; var remaining = 0d;
            for (var i = start; i < start + StatusCapacityPerActor; i++) {
                if (this.instances[i].Status == null) continue;
                count++; remaining = Math.Max(remaining, this.instances[i].ExpiresAt - this.time);
            }
            var offset = row * this.statCount;
            return new PlayerStatsSnapshot(count, (float)Math.Max(0d, remaining), this.finals[offset + this.speedIndex], this.finals[offset + this.armorIndex]);
        }
        private void RemoveAt(int index) {
            var activeIndex = this.instances[index].ActiveIndex;
            var last = this.activeIndices[--this.activeCount];
            this.activeIndices[activeIndex] = last;
            this.instances[last].ActiveIndex = activeIndex;
            this.instances[index] = default;
            this.MarkDirty(index / StatusCapacityPerActor);
        }
        private void MarkDirty(int row) {
            if (this.dirty[row]) return;
            this.dirty[row] = true;
            this.dirtyRows[this.dirtyCount++] = row;
        }
        private int GetRow(Entity target) { this.CheckAlive(); return this.bindings.GetRow(target, this.rowCount); }
        public bool CanReceive(Entity target) {
            this.CheckAlive();
            return this.bindings.IsRegistered(target) && this.bindings.IsLiving(this.GetRow(target));
        }
        public void EnsureCapacity() {
            this.CheckAlive();
            if (this.freeCount > 0 || this.rowCount < this.bindings.Capacity) return;
            for (var row = 0; row < this.rowCount; row++) if (this.bindings.IsAllocated(row) && !this.bindings.IsValid(row)) this.ReleaseRow(row);
            if (this.freeCount == 0) throw new InvalidOperationException("Concurrent stat owner capacity exhausted.");
        }
        public void Release(Entity target) {
            this.CheckAlive();
            if (!this.bindings.HasSlot(target)) return;
            var row = this.GetRow(target);
            this.bindings.RemoveSlot(target);
            this.ReleaseRow(row);
        }
        private void ReleaseRow(int row) {
            var start = row * StatusCapacityPerActor;
            for (var i = start; i < start + StatusCapacityPerActor; i++) if (this.instances[i].Status != null) this.RemoveAt(i);
            this.bindings.ClearRow(row);
            Array.Clear(this.bases, row * this.statCount, this.statCount);
            Array.Clear(this.finals, row * this.statCount, this.statCount);
            this.freeRows[this.freeCount++] = row;
        }
        private static void Finite(float value) {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
        }
        private void CheckAlive() { if (this.disposed) throw new ObjectDisposedException(nameof(StatStorage)); }
        public void Dispose() {
            if (this.disposed) return;
            this.disposed = true;
            Array.Clear(this.instances, 0, this.instances.Length);
            this.bindings.Dispose();
            this.definitions.Clear();
            this.activeCount = this.dirtyCount = 0;
        }
    }
}

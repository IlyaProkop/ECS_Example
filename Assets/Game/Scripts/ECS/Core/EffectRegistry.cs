using System;
using System.Collections.Generic;

namespace Game.ECS.Core {
    // Type dispatch is confined to composition; execution uses compiled arrays.
    internal sealed class EffectRegistry<TDefinition, TExecutor> where TExecutor : class, IDisposable {
        private readonly Dictionary<Type, Func<TDefinition, TExecutor>> factories = new();
        public void Register<T>(Func<T, TExecutor> factory) where T : TDefinition {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (!this.factories.TryAdd(typeof(T), value => factory((T)value)))
                throw new InvalidOperationException($"Effect {typeof(T).Name} is already registered.");
        }
        public TExecutor[] Compile(IReadOnlyList<TDefinition> definitions) {
            var result = new TExecutor[definitions.Count];
            try {
                for (var i = 0; i < result.Length; i++) {
                    var definition = definitions[i];
                    if (!this.factories.TryGetValue(definition.GetType(), out var factory))
                        throw new InvalidOperationException($"No executor registered for {definition.GetType().Name}.");
                    result[i] = factory(definition) ?? throw new InvalidOperationException("Effect factory returned null.");
                }
                return result;
            } catch {
                foreach (var executor in result) executor?.Dispose();
                throw;
            }
        }
    }
}

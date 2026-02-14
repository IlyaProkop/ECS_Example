using System;
using Game.Domain;
using NUnit.Framework;

namespace Game.Tests {
    public sealed class ApplicationCompositionTests {
        [Test]
        public void ReconfiguringResolvedServicesPreservesTheActiveApplication() {
            var store = new MemoryStore();
            ApplicationServices.SetProgressStoreForTests(store);
            try {
                var flow = ApplicationServices.Flow;
                var progress = ApplicationServices.Progress;
                Assert.Throws<InvalidOperationException>(() => ApplicationServices.ConfigureProgressStore(new MemoryStore()));
                Assert.That(ApplicationServices.Flow, Is.SameAs(flow));
                Assert.That(ApplicationServices.Progress, Is.SameAs(progress));
                Assert.That(progress.Current.currency, Is.EqualTo(17));
                Assert.That(store.Saves, Is.Zero);
            } finally { ApplicationServices.SetProgressStoreForTests(new MemoryStore()); }
        }
        private sealed class MemoryStore : IProgressStore {
            public int Saves;
            public PlayerProgress Load() => new PlayerProgress { currency = 17 };
            public void Save(PlayerProgress progress) => this.Saves++;
        }
    }
}

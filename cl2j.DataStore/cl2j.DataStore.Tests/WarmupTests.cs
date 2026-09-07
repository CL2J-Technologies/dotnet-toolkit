using cl2j.DataStore.Dictionary;
using cl2j.DataStore.List;
using Xunit;

namespace cl2j.DataStore.Tests
{
    /// <summary>
    ///     A registry knows every store it was given, but not how to load one: the load interfaces
    ///     are generic, so a caller wanting to warm everything had to name each store again with
    ///     its type arguments — a second copy of what registration already said, and one that rots
    ///     silently the first time somebody adds a store and forgets the line.
    ///
    ///     <para>
    ///     <see cref="IDataStoreWarmable"/> is the non-generic handle that removes that. What it
    ///     deliberately does not carry is a policy: when to warm, how long to wait, and whether a
    ///     store that cannot be read should stop an application from starting are decisions an
    ///     application makes, not a library.
    ///     </para>
    /// </summary>
    public class WarmupTests
    {
        private static readonly TimeSpan NoRefresh = TimeSpan.FromMinutes(10);

        private static DataStoreDictionaryFactory DictionaryFactory() => new(new RecordingLogger<DataStoreDictionaryFactory>());

        private static DataStoreListFactory ListFactory() => new(new RecordingLogger<DataStoreListFactory>());

        [Fact]
        public async Task A_registered_cache_offers_itself_for_warming_under_its_name()
        {
            var store = new InMemoryDictionaryStore<string, Person>();
            store.Seed("1", new Person("1", "Renée"));
            using var cache = new DataStoreDictionaryCommandAndQueryCache<string, Person>("people", store, NoRefresh, new RecordingLogger());

            var factory = DictionaryFactory();
            factory.AddDataStoreDictionaryCommandAndQuery("people", cache);

            var warmable = Assert.Single(factory.GetWarmable());

            Assert.Equal("people", warmable.Name);
            await warmable.WarmAsync();
        }

        [Fact]
        public void A_store_registered_without_a_cache_is_not_offered()
        {
            //Nothing to warm: it reads its source on every call. Offering it would make a warm-up
            //do a pointless read at startup.
            var factory = DictionaryFactory();
            factory.AddDataStoreDictionaryCommandAndQuery("people", new InMemoryDictionaryStore<string, Person>());

            Assert.Empty(factory.GetWarmable());
        }

        [Fact]
        public void Both_registries_offer_what_they_hold_and_nothing_else()
        {
            var dictionaries = DictionaryFactory();
            var lists = ListFactory();

            using var dictionaryCache = new DataStoreDictionaryCommandAndQueryCache<string, Person>(
                "people", new InMemoryDictionaryStore<string, Person>(), NoRefresh, new RecordingLogger());
            using var listCache = new DataStoreListLoadCache<Person>(
                "archive", new InMemoryListStore<string, Person>(p => p.Id), NoRefresh, new RecordingLogger());

            dictionaries.AddDataStoreDictionaryCommandAndQuery("people", dictionaryCache);
            lists.AddDataStoreListLoad("archive", listCache);

            Assert.Equal("people", Assert.Single(dictionaries.GetWarmable()).Name);
            Assert.Equal("archive", Assert.Single(lists.GetWarmable()).Name);
        }

        [Fact]
        public async Task Warming_does_not_finish_before_the_load_does()
        {
            //The whole point: an application can wait for this and know the cache is populated
            //rather than merely started.
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var store = new InMemoryDictionaryStore<string, Person>
            {
                Gate = async () => await release.Task
            };
            store.Seed("1", new Person("1", "Renée"));

            using var cache = new DataStoreDictionaryCommandAndQueryCache<string, Person>("people", store, NoRefresh, new RecordingLogger());
            var factory = DictionaryFactory();
            factory.AddDataStoreDictionaryCommandAndQuery("people", cache);

            var warming = Assert.Single(factory.GetWarmable()).WarmAsync();

            var raced = await Task.WhenAny(warming, Task.Delay(TimeSpan.FromMilliseconds(300)));
            Assert.NotSame(warming, raced);

            release.SetResult();
            await warming;

            Assert.Equal("Renée", (await cache.GetByIdAsync("1"))?.Name);
        }

        [Fact]
        public async Task Warming_a_source_that_cannot_be_read_reports_it()
        {
            //It does not swallow. Whether that should stop an application from starting is the
            //caller's decision, and it can only make it if it is told.
            using var cache = new UnreadableCache(new RecordingLogger());
            var factory = ListFactory();
            factory.AddDataStoreListLoad("broken", cache);

            var warmable = Assert.Single(factory.GetWarmable());

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => warmable.WarmAsync());
            Assert.Contains("broken", failure.Message, StringComparison.Ordinal);
        }

        private sealed class UnreadableCache(RecordingLogger logger)
            : DataStoreListLoadCache<Person>("broken", new UnreadableListStore<Person>(), NoRefresh, logger)
        {
        }
    }
}

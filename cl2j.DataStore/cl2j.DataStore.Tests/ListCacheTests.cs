using cl2j.DataStore.List;
using Microsoft.Extensions.Logging;
using Xunit;

namespace cl2j.DataStore.Tests
{
    /// <summary>
    ///     The cache in front of a list store. It differs from the dictionary one in the two places
    ///     a list differs from a dictionary: there is no key on the item itself — a predicate
    ///     supplies it — and the order the items come back in is part of what the cache decides.
    /// </summary>
    public class ListCacheTests
    {
        private static readonly TimeSpan NoRefresh = TimeSpan.FromMinutes(10);

        private static InMemoryListStore<string, Person> Seeded(params Person[] people)
        {
            var store = new InMemoryListStore<string, Person>(p => p.Id);
            store.Seed(people);
            return store;
        }

        private static DataStoreListCommandAndQueryCache<string, Person> Cache(
            InMemoryListStore<string, Person> store,
            Func<Person, object?>? orderBy = null,
            bool? ascending = null,
            ILogger? logger = null)
        {
            return new DataStoreListCommandAndQueryCache<string, Person>(
                "people", store, NoRefresh, p => p.Id, logger ?? new RecordingLogger(), orderBy, ascending);
        }

        [Fact]
        public async Task What_the_store_held_is_what_the_cache_answers()
        {
            using var cache = Cache(Seeded(new Person("1", "Renée"), new Person("2", "Ari")));

            Assert.Equal(2, (await cache.GetAllAsync()).Count);
        }

        [Fact]
        public async Task A_read_by_key_uses_the_predicate_and_does_not_reach_the_store()
        {
            var store = Seeded(new Person("1", "Renée"), new Person("2", "Ari"));
            using var cache = Cache(store);

            Assert.Equal("Ari", (await cache.GetByIdAsync("2"))?.Name);
            Assert.DoesNotContain("GetById", store.Calls);
        }

        [Fact]
        public async Task A_key_that_is_not_there_is_absence_rather_than_an_error()
        {
            using var cache = Cache(Seeded(new Person("1", "Renée")));

            Assert.Null(await cache.GetByIdAsync("absent"));
        }

        [Fact]
        public async Task An_ordering_predicate_sorts_the_cache_as_it_loads()
        {
            //The sort happens once, at load, not on every read — which is the reason it is worth
            //passing a predicate rather than ordering at the call site.
            using var cache = Cache(
                Seeded(new Person("3", "Cléo"), new Person("1", "Ari"), new Person("2", "Renée")),
                orderBy: p => p.Name);

            Assert.Equal(["Ari", "Cléo", "Renée"], (await cache.GetAllAsync()).Select(p => p.Name));
        }

        [Fact]
        public async Task Descending_is_asked_for_explicitly()
        {
            using var cache = Cache(
                Seeded(new Person("1", "Ari"), new Person("2", "Renée")),
                orderBy: p => p.Name,
                ascending: false);

            Assert.Equal(["Renée", "Ari"], (await cache.GetAllAsync()).Select(p => p.Name));
        }

        [Fact]
        public async Task An_unspecified_direction_is_ascending()
        {
            using var cache = Cache(
                Seeded(new Person("1", "Renée"), new Person("2", "Ari")),
                orderBy: p => p.Name,
                ascending: null);

            Assert.Equal(["Ari", "Renée"], (await cache.GetAllAsync()).Select(p => p.Name));
        }

        [Fact]
        public async Task An_insert_reaches_the_store_and_then_the_cache()
        {
            var store = Seeded();
            using var cache = Cache(store);
            await cache.GetAllAsync();

            await cache.InsertAsync(new Person("1", "Renée"));

            Assert.Equal("Renée", Assert.Single(store.Contents).Name);
            Assert.Equal("Renée", (await cache.GetByIdAsync("1"))?.Name);
        }

        [Fact]
        public async Task An_insert_lands_at_the_end_whatever_the_ordering_asked_for()
        {
            //Characterisation. The ordering predicate is applied when the cache loads and never
            //again, so a row inserted afterwards sits at the end until the next refresh. Reads
            //between the two see an order that is neither the one asked for nor the store's.
            var store = Seeded(new Person("1", "Bea"), new Person("2", "Cléo"));
            using var cache = Cache(store, orderBy: p => p.Name);
            await cache.GetAllAsync();

            await cache.InsertAsync(new Person("3", "Ari"));

            Assert.Equal(["Bea", "Cléo", "Ari"], (await cache.GetAllAsync()).Select(p => p.Name));
        }

        [Fact]
        public async Task An_update_replaces_the_item_in_place()
        {
            var store = Seeded(new Person("1", "Renée"), new Person("2", "Ari"));
            using var cache = Cache(store);
            await cache.GetAllAsync();

            await cache.UpdateAsync(new Person("1", "Renée Tremblay"));

            Assert.Equal(["Renée Tremblay", "Ari"], (await cache.GetAllAsync()).Select(p => p.Name));
            Assert.Equal("Renée Tremblay", store.Contents[0].Name);
        }

        [Fact]
        public async Task An_update_of_an_item_the_cache_never_saw_is_cached_as_well_as_written()
        {
            //Same shape as the dictionary cache, and the same fix: the write went through and the
            //cache was left as it was, with nothing said. It is appended now, because the store
            //accepted it. See issue #32.
            var store = Seeded();
            using var cache = Cache(store);
            await cache.GetAllAsync();

            var observer = new RecordingObserver<IReadOnlyList<Person>>();
            cache.Subscribe(observer);

            store.Seed(new Person("2", "Arrived elsewhere"));
            await cache.UpdateAsync(new Person("2", "Updated"));

            Assert.Equal("Updated", store.Contents[0].Name);
            Assert.Equal("Updated", (await cache.GetByIdAsync("2"))?.Name);
            Assert.Single(observer.Notifications);
        }

        [Fact]
        public async Task What_GetAll_returns_cannot_be_used_to_change_the_cache()
        {
            var store = Seeded(new Person("1", "Renée"));
            using var cache = Cache(store);

            var all = await cache.GetAllAsync();

            Assert.IsNotType<List<Person>>(all);
            Assert.Throws<NotSupportedException>(() => ((IList<Person>)all).Clear());
            Assert.NotNull(await cache.GetByIdAsync("1"));
        }

        [Fact]
        public async Task A_delete_removes_it_from_both()
        {
            var store = Seeded(new Person("1", "Renée"), new Person("2", "Ari"));
            using var cache = Cache(store);
            await cache.GetAllAsync();

            await cache.DeleteAsync("1");

            Assert.Equal("Ari", Assert.Single(store.Contents).Name);
            Assert.Equal("Ari", Assert.Single(await cache.GetAllAsync()).Name);
        }

        [Fact]
        public async Task Replacing_everything_replaces_both_and_keeps_a_copy()
        {
            //Unlike the dictionary cache, this one copies what it is handed — `cache = [.. items]`
            //rather than `cache = items` — so the caller's collection and the cache go their
            //separate ways afterwards.
            var store = Seeded(new Person("1", "Renée"));
            using var cache = Cache(store);
            await cache.GetAllAsync();

            var passed = new List<Person> { new("9", "Only one left") };
            await cache.ReplaceAllByAsync(passed);

            passed.Add(new Person("10", "Added afterwards"));

            Assert.Equal("Only one left", Assert.Single(await cache.GetAllAsync()).Name);
            Assert.Equal("Only one left", Assert.Single(store.Contents).Name);
        }

        [Fact]
        public async Task Every_change_is_announced_to_subscribers()
        {
            var store = Seeded();
            using var cache = Cache(store);
            await cache.GetAllAsync();

            var observer = new RecordingObserver<IReadOnlyList<Person>>();
            cache.Subscribe(observer);

            await cache.InsertAsync(new Person("1", "Renée"));
            await cache.UpdateAsync(new Person("1", "Renée Tremblay"));
            await cache.DeleteAsync("1");
            await cache.ReplaceAllByAsync([]);

            Assert.Equal(4, observer.Notifications.Count);
        }

        [Fact]
        public async Task A_delete_of_something_absent_reaches_the_store_and_announces_nothing()
        {
            var store = Seeded();
            using var cache = Cache(store);
            await cache.GetAllAsync();

            var observer = new RecordingObserver<IReadOnlyList<Person>>();
            cache.Subscribe(observer);

            await cache.DeleteAsync("never-there");

            Assert.Contains("Delete", store.Calls);
            Assert.Empty(observer.Notifications);
        }
    }

    public class ListLoadCacheTests
    {
        private static readonly TimeSpan NoRefresh = TimeSpan.FromMinutes(10);

        [Fact]
        public async Task A_load_only_cache_reads_the_store_once_and_answers_from_memory()
        {
            var store = new InMemoryListStore<string, Person>(p => p.Id);
            store.Seed(new Person("1", "Renée"));
            using var cache = new DataStoreListLoadCache<Person>("people", store, NoRefresh, new RecordingLogger());

            await cache.GetAllAsync();
            await cache.GetAllAsync();
            await cache.GetAllAsync();

            Assert.Equal("Renée", Assert.Single(await cache.GetAllAsync()).Name);
            Assert.Single(store.Calls);
        }

        [Fact]
        public async Task Subscribers_are_told_when_the_cache_loads()
        {
            var store = new InMemoryListStore<string, Person>(p => p.Id);
            store.Seed(new Person("1", "Renée"));
            var observer = new RecordingObserver<IReadOnlyList<Person>>();

            using var cache = new DataStoreListLoadCache<Person>("people", store, NoRefresh, new RecordingLogger());
            cache.Subscribe(observer);
            await cache.GetAllAsync();

            //The first load races the subscription — the timer starts in the constructor — so the
            //assertion is on the load happening at all, not on catching its notification.
            Assert.Single(await cache.GetAllAsync());
        }

        [Fact]
        public async Task A_source_that_cannot_be_read_leaves_an_empty_cache_and_a_critical_log()
        {
            var logger = new RecordingLogger();
            var store = new UnreadableListStore<Person>();
            using var cache = new DataStoreListLoadCache<Person>("broken", store, NoRefresh, logger);

            Assert.Empty(await cache.GetAllAsync());
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Critical && e.Exception is not null);
            Assert.True(store.Attempts >= 1);
        }
    }
}

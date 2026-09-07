using cl2j.DataStore.Dictionary;
using Microsoft.Extensions.Logging;
using Xunit;

namespace cl2j.DataStore.Tests
{
    /// <summary>
    ///     The cache in front of a dictionary store. Two things are worth testing and neither is
    ///     the dictionary: that every write reaches the store underneath, and that the cache and
    ///     the store still agree afterwards. Where they can stop agreeing is written down as a
    ///     test rather than left to be discovered.
    ///
    ///     <para>
    ///     The refresh interval is deliberately long. CacheLoader's WaitAsync gives up after one
    ///     interval and returns an unloaded cache without saying so, so a short interval here would
    ///     be a race rather than a test.
    ///     </para>
    /// </summary>
    public class DictionaryCacheTests
    {
        private static readonly TimeSpan NoRefresh = TimeSpan.FromMinutes(10);

        private static DataStoreDictionaryCommandAndQueryCache<string, Person> Cache(
            InMemoryDictionaryStore<string, Person> store,
            ILogger? logger = null)
        {
            return new DataStoreDictionaryCommandAndQueryCache<string, Person>(
                "people", store, NoRefresh, logger ?? new RecordingLogger());
        }

        private static InMemoryDictionaryStore<string, Person> Seeded(params Person[] people)
        {
            var store = new InMemoryDictionaryStore<string, Person>();
            foreach (var person in people)
                store.Seed(person.Id, person);
            return store;
        }

        [Fact]
        public async Task What_the_store_held_is_what_the_cache_answers()
        {
            var store = Seeded(new Person("1", "Renée"), new Person("2", "Ari"));
            using var cache = Cache(store);

            var all = await cache.GetAllAsync();

            Assert.Equal(2, all.Count);
            Assert.Equal("Renée", all["1"].Name);
        }

        [Fact]
        public async Task A_read_by_key_does_not_reach_the_store()
        {
            //The whole point of the class. If this stops holding, every read becomes a round trip
            //and nothing fails — it just gets slower.
            var store = Seeded(new Person("1", "Renée"));
            using var cache = Cache(store);

            var person = await cache.GetByIdAsync("1");

            Assert.Equal("Renée", person?.Name);
            Assert.DoesNotContain(store.Calls, c => c.StartsWith("GetById", StringComparison.Ordinal));
        }

        [Fact]
        public async Task A_key_that_is_not_there_is_absence_rather_than_an_error()
        {
            using var cache = Cache(Seeded(new Person("1", "Renée")));

            Assert.Null(await cache.GetByIdAsync("absent"));
        }

        [Fact]
        public async Task An_insert_reaches_the_store_and_then_the_cache()
        {
            //In that order: the cache is only updated once the store has accepted the write, so a
            //store that refuses leaves the cache untouched rather than ahead of it.
            var store = Seeded();
            using var cache = Cache(store);
            await cache.GetAllAsync();

            await cache.InsertAsync("1", new Person("1", "Renée"));

            Assert.Equal("Renée", store.Contents["1"].Name);
            Assert.Equal("Renée", (await cache.GetByIdAsync("1"))?.Name);
        }

        [Fact]
        public async Task A_store_that_refuses_the_insert_leaves_the_cache_alone()
        {
            var store = Seeded(new Person("1", "Renée"));
            using var cache = Cache(store);
            await cache.GetAllAsync();

            //The store enforces what the interface says: the key must not exist.
            await Assert.ThrowsAsync<ArgumentException>(() => cache.InsertAsync("1", new Person("1", "Someone else")));

            Assert.Equal("Renée", (await cache.GetByIdAsync("1"))?.Name);
        }

        [Fact]
        public async Task A_store_that_accepts_a_duplicate_insert_leaves_the_two_disagreeing()
        {
            //Characterisation, and the sharp edge of the ordering above. When the store upserts —
            //plenty do — the write succeeds and then cache.Add throws on the key it already has.
            //The store now holds the new value, the cache still holds the old one, and the caller
            //sees an exception that says nothing about either.
            var store = Seeded(new Person("1", "Renée"));
            store.UpsertOnInsert = true;
            using var cache = Cache(store);
            await cache.GetAllAsync();

            await Assert.ThrowsAsync<ArgumentException>(() => cache.InsertAsync("1", new Person("1", "Someone else")));

            Assert.Equal("Someone else", store.Contents["1"].Name);
            Assert.Equal("Renée", (await cache.GetByIdAsync("1"))?.Name);
        }

        [Fact]
        public async Task An_update_reaches_the_store_and_the_cache()
        {
            var store = Seeded(new Person("1", "Renée"));
            using var cache = Cache(store);
            await cache.GetAllAsync();

            await cache.UpdateAsync("1", new Person("1", "Renée Tremblay"));

            Assert.Equal("Renée Tremblay", store.Contents["1"].Name);
            Assert.Equal("Renée Tremblay", (await cache.GetByIdAsync("1"))?.Name);
        }

        [Fact]
        public async Task An_update_of_a_key_the_cache_never_saw_is_written_but_not_cached()
        {
            //Characterisation. UpdateAsync only touches the cache when the key is already in it, so
            //a row that appeared in the store after the last refresh — inserted by another process,
            //or by another instance of this application — is written and then stays invisible until
            //the next refresh. No exception, no notification.
            var store = Seeded();
            using var cache = Cache(store);
            await cache.GetAllAsync();

            store.Seed("2", new Person("2", "Arrived elsewhere"));
            await cache.UpdateAsync("2", new Person("2", "Updated"));

            Assert.Equal("Updated", store.Contents["2"].Name);
            Assert.Null(await cache.GetByIdAsync("2"));
        }

        [Fact]
        public async Task A_delete_reaches_the_store_and_the_cache()
        {
            var store = Seeded(new Person("1", "Renée"), new Person("2", "Ari"));
            using var cache = Cache(store);
            await cache.GetAllAsync();

            await cache.DeleteAsync("1");

            Assert.False(store.Contents.ContainsKey("1"));
            Assert.Null(await cache.GetByIdAsync("1"));
            Assert.NotNull(await cache.GetByIdAsync("2"));
        }

        [Fact]
        public async Task Replacing_everything_replaces_both()
        {
            var store = Seeded(new Person("1", "Renée"));
            using var cache = Cache(store);
            await cache.GetAllAsync();

            await cache.ReplaceAllByAsync(new Dictionary<string, Person> { ["9"] = new Person("9", "Only one left") });

            Assert.Equal(["9"], store.Contents.Keys);
            Assert.Equal(["9"], (await cache.GetAllAsync()).Keys);
        }

        [Fact]
        public async Task The_dictionary_handed_to_ReplaceAll_becomes_the_cache_itself()
        {
            //Characterisation. `cache = items` keeps the caller's own dictionary rather than a copy
            //of it, so whoever passed it can still change what the cache holds — without the
            //semaphore, and without anyone being notified.
            var store = Seeded();
            using var cache = Cache(store);
            await cache.GetAllAsync();

            var passed = new Dictionary<string, Person> { ["1"] = new Person("1", "Renée") };
            await cache.ReplaceAllByAsync(passed);

            passed["2"] = new Person("2", "Added behind the cache's back");

            Assert.Equal(2, (await cache.GetAllAsync()).Count);
            Assert.Single(store.Contents);
        }

        [Fact]
        public async Task What_GetAll_returns_is_the_cache_and_not_a_copy_of_it()
        {
            //Characterisation, and the same hole seen from the other side: a caller that mutates
            //the returned dictionary is mutating the cache.
            var store = Seeded(new Person("1", "Renée"));
            using var cache = Cache(store);

            (await cache.GetAllAsync()).Remove("1");

            Assert.Null(await cache.GetByIdAsync("1"));
            Assert.True(store.Contents.ContainsKey("1"));
        }

        [Fact]
        public async Task Every_change_is_announced_to_subscribers()
        {
            var store = Seeded();
            using var cache = Cache(store);
            await cache.GetAllAsync();

            var observer = new RecordingObserver<Dictionary<string, Person>>();
            cache.Subscribe(observer);

            await cache.InsertAsync("1", new Person("1", "Renée"));
            await cache.UpdateAsync("1", new Person("1", "Renée Tremblay"));
            await cache.DeleteAsync("1");
            await cache.ReplaceAllByAsync([]);

            Assert.Equal(4, observer.Notifications.Count);
        }

        [Fact]
        public async Task A_change_that_the_cache_did_not_make_is_not_announced()
        {
            //Deleting a key the cache does not hold still reaches the store, but there is nothing
            //to tell subscribers about.
            var store = Seeded();
            using var cache = Cache(store);
            await cache.GetAllAsync();

            var observer = new RecordingObserver<Dictionary<string, Person>>();
            cache.Subscribe(observer);

            await cache.DeleteAsync("never-there");

            Assert.Contains("Delete(never-there)", store.Calls);
            Assert.Empty(observer.Notifications);
        }

        [Fact]
        public async Task A_source_that_cannot_be_read_leaves_an_empty_cache_and_a_critical_log()
        {
            //The failure mode a caller has to know about: the load swallows the exception, so
            //GetAllAsync answers "nothing" rather than failing. An empty answer and an unavailable
            //source look identical from the outside.
            var logger = new RecordingLogger();
            using var cache = new UnreadableCache(logger);

            var all = await cache.GetAllAsync();

            Assert.Empty(all);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Critical && e.Exception is not null);
        }

        private sealed class UnreadableCache(ILogger logger)
            : DataStoreDictionaryLoadCache<string, Person>("broken", new UnreadableDictionaryStore<string, Person>(), NoRefresh, logger)
        {
        }
    }

    /// <summary>
    ///     A type of its own, so the static semaphore this exercises is not the one every other
    ///     test in this assembly is using. That statics are shared per closed generic type is
    ///     exactly what is being shown.
    /// </summary>
    public sealed class Contended
    {
        public string Id { get; init; } = string.Empty;
    }

    public class DictionaryCacheContentionTests
    {
        [Fact]
        public async Task One_store_writing_blocks_an_unrelated_store_from_writing()
        {
            //Characterisation, not endorsement. The semaphore guarding the cache is declared
            //static on DataStoreDictionaryLoadCache<TKey, TValue>, so there is one per closed
            //generic type rather than one per store. Two entirely separate stores that happen to
            //share type arguments serialise against each other: a slow write to one holds up every
            //write to the other.
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstIsInside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var slowStore = new InMemoryDictionaryStore<string, Contended>
            {
                Gate = async () =>
                {
                    firstIsInside.TrySetResult();
                    await held.Task;
                }
            };
            var unrelatedStore = new InMemoryDictionaryStore<string, Contended>();

            using var slow = new DataStoreDictionaryCommandAndQueryCache<string, Contended>(
                "slow", slowStore, TimeSpan.FromMinutes(10), new RecordingLogger());
            using var unrelated = new DataStoreDictionaryCommandAndQueryCache<string, Contended>(
                "unrelated", unrelatedStore, TimeSpan.FromMinutes(10), new RecordingLogger());

            var blocking = slow.InsertAsync("1", new Contended { Id = "1" });
            await firstIsInside.Task;

            var other = unrelated.InsertAsync("2", new Contended { Id = "2" });
            var finished = await Task.WhenAny(other, Task.Delay(TimeSpan.FromMilliseconds(500)));

            Assert.NotSame(other, finished);
            Assert.Empty(unrelatedStore.Contents);

            held.SetResult();
            await blocking;
            await other;

            Assert.Single(unrelatedStore.Contents);
        }
    }
}

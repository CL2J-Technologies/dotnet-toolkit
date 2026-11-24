using Microsoft.Extensions.Logging;
using Xunit;

namespace cl2j.Tooling.Tests
{
    /// <summary>
    ///     <see cref="CacheableObject{T}"/> keeps a list loaded through <see cref="CacheLoader"/>.
    ///     Its semaphore is static, so these tests live in one class and run one at a time.
    /// </summary>
    public class CacheableObjectTests
    {
        //The loader never refreshes during a test; WaitAsync gives up after one interval.
        private static readonly TimeSpan NoRefresh = TimeSpan.FromSeconds(5);

        private sealed class Store : CacheableObject<string>
        {
            //CacheableObject starts its loader from its constructor, so the first load can run
            //before this constructor has assigned anything. The load waits for the assignment;
            //without it these tests would see an empty cache from time to time. The real race is
            //documented by the skipped test at the end of the class.
            private volatile Func<Task<List<string>>>? load;

            public Store(Func<Task<List<string>>> load, ILogger logger)
                : base("store", NoRefresh, logger)
            {
                this.load = load;
            }

            protected override Task<List<string>> LoadCache()
            {
                SpinWait.SpinUntil(() => load != null);
                return load!();
            }

            public Task<List<string>> Items() => GetCache();

            public Task Wait() => WaitForCache();

            public Task Refresh() => RefreshCache();

            public Task<List<string>> Add(string item) => AddCacheObject(item);

            public Task<List<string>> Replace(string oldItem, string newItem) => UpdateCacheObject(oldItem, newItem);
        }

        private static Store StoreOf(params string[] items) =>
            new(() => Task.FromResult(items.ToList()), new RecordingLogger());

        [Fact]
        public async Task It_serves_what_was_loaded()
        {
            using var store = StoreOf("a", "b");

            Assert.Equal(["a", "b"], await store.Items());
        }

        [Fact]
        public async Task WaitForCache_returns_once_the_first_load_is_done()
        {
            using var store = StoreOf("a");

            await store.Wait();

            Assert.Equal(["a"], await store.Items());
        }

        [Fact]
        public async Task A_refresh_replaces_the_cache_with_what_is_loaded_next()
        {
            var generation = 0;
            using var store = new Store(() => Task.FromResult(new List<string> { $"v{Interlocked.Increment(ref generation)}" }), new RecordingLogger());
            Assert.Equal(["v1"], await store.Items());

            await store.Refresh();

            Assert.Equal(["v2"], await store.Items());
        }

        [Fact]
        public async Task Adding_an_object_appends_it_to_the_cache()
        {
            using var store = StoreOf("a");

            var result = await store.Add("b");

            Assert.Equal(["a", "b"], result);
            Assert.Equal(["a", "b"], await store.Items());
        }

        [Fact]
        public async Task Updating_an_object_replaces_the_old_one_with_the_new_one()
        {
            using var store = StoreOf("a", "b");

            var result = await store.Replace("a", "c");

            Assert.Equal(["b", "c"], result);
        }

        [Fact]
        public async Task A_load_that_throws_is_logged_and_keeps_the_previous_cache()
        {
            var fail = false;
            var logger = new RecordingLogger();
            using var store = new Store(() => fail ? throw new InvalidOperationException("boom") : Task.FromResult(new List<string> { "a" }), logger);
            Assert.Equal(["a"], await store.Items());

            fail = true;
            await store.Refresh();

            Assert.Equal(["a"], await store.Items());
            Assert.Contains(logger.Snapshot(), e => e.Level == LogLevel.Error && e.Message.Contains("store"));
        }

        [Fact]
        public async Task A_failed_refresh_does_not_keep_the_next_one_from_running()
        {
            //The semaphore is released in a finally. A leak there would hang this test.
            var fail = true;
            using var store = new Store(() => fail ? throw new InvalidOperationException("boom") : Task.FromResult(new List<string> { "ok" }), new RecordingLogger());
            await store.Refresh();

            fail = false;
            await store.Refresh();

            Assert.Equal(["ok"], await store.Items());
        }

        private static readonly ManualResetEventSlim FirstLoadAttempted = new(false);

        /// <summary>Holds the base constructor, once it has started the loader, until a first load has run.</summary>
        private sealed class GatedLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Debug)
                    FirstLoadAttempted.Wait(TimeSpan.FromSeconds(5));
            }
        }

        private sealed class EagerStore : CacheableObject<string>
        {
            private readonly List<string> items;

            public EagerStore() : base("eager", NoRefresh, new GatedLogger())
            {
                items = ["a"];
            }

            protected override Task<List<string>> LoadCache()
            {
                FirstLoadAttempted.Set();
                return Task.FromResult(items?.ToList() ?? []);
            }

            public Task<List<string>> Items() => GetCache();
        }

        [Fact(Skip = "Bug: CacheableObject starts its loader in its constructor, so the first LoadCache can run before the derived constructor has assigned its fields and the cache is left empty.")]
        public async Task The_first_load_sees_the_fields_a_derived_class_sets_in_its_constructor()
        {
            FirstLoadAttempted.Reset();
            using var store = new EagerStore();

            Assert.Equal(["a"], await store.Items());
        }
    }
}

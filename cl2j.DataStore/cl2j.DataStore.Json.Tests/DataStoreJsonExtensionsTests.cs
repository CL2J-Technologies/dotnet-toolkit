using cl2j.DataStore.Dictionary;
using cl2j.DataStore.Json.Dictionary;
using cl2j.DataStore.Json.List;
using cl2j.DataStore.List;
using cl2j.FileStorage.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace cl2j.DataStore.Json.Tests
{
    /// <summary>
    ///     The wiring: each extension resolves a provider by name from the service provider, builds
    ///     the store (and the cache around it when asked), and the Add variants register it in the
    ///     factory under the name given.
    /// </summary>
    public sealed class DataStoreJsonExtensionsTests : IDisposable
    {
        private static readonly TimeSpan NoRefresh = TimeSpan.FromMinutes(10);

        private const string PeopleJson = """[{"id":"2","name":"Turing"},{"id":"1","name":"Lovelace"}]""";

        private readonly JsonStoreEnvironment environment = new();
        private readonly List<IDisposable> disposables = [];

        public void Dispose()
        {
            foreach (var disposable in disposables)
                disposable.Dispose();
            environment.Dispose();
        }

        private T Track<T>(T value)
        {
            if (value is IDisposable disposable)
                disposables.Add(disposable);
            return value;
        }

        [Fact]
        public async Task A_list_command_and_query_store_is_built_over_the_named_file()
        {
            await environment.Provider.WriteTextAsync("people.json", PeopleJson);

            var store = environment.Services.CreateDataStoreListCommandAndQueryJson<string, PersonEntity>(JsonStoreEnvironment.StorageName, "people.json");

            Assert.Equal("Lovelace", (await store.GetByIdAsync("1"))?.Name);
        }

        [Fact]
        public async Task A_list_command_and_query_store_can_take_its_own_key_predicate()
        {
            await environment.Provider.WriteTextAsync("people.json", PeopleJson);

            var store = environment.Services.CreateDataStoreListCommandAndQueryJson<string, PersonEntity>(JsonStoreEnvironment.StorageName, "people.json", p => p.Name);

            Assert.Equal("2", (await store.GetByIdAsync("Turing"))?.Id);
        }

        [Fact]
        public void A_list_command_and_query_store_over_an_unknown_storage_is_refused()
        {
            Assert.ThrowsAny<Exception>(() => environment.Services.CreateDataStoreListCommandAndQueryJson<string, PersonEntity>("unknown", "people.json"));
        }

        [Fact]
        public async Task A_cached_list_command_and_query_store_answers_from_the_file_and_writes_through()
        {
            await environment.Provider.WriteTextAsync("people.json", PeopleJson);

            var cache = Track(environment.Services.CreateDataStoreListCommandAndQueryJsonWithCache<string, PersonEntity>("people", JsonStoreEnvironment.StorageName, "people.json", NoRefresh));

            Assert.Equal(["Turing", "Lovelace"], (await cache.GetAllAsync()).Select(p => p.Name));

            await cache.InsertAsync(new PersonEntity { Id = "3", Name = "Hopper" });

            Assert.Contains("Hopper", await environment.Provider.ReadTextAsync("people.json"));
            Assert.Equal("Hopper", (await cache.GetByIdAsync("3"))?.Name);
        }

        [Fact]
        public async Task A_cached_list_command_and_query_store_can_take_its_own_key_predicate()
        {
            await environment.Provider.WriteTextAsync("people.json", PeopleJson);

            var cache = Track(environment.Services.CreateDataStoreListCommandAndQueryJsonWithCache<string, PersonEntity>("people", JsonStoreEnvironment.StorageName, "people.json", p => p.Name, NoRefresh));

            Assert.Equal("2", (await cache.GetByIdAsync("Turing"))?.Id);
        }

        [Fact]
        public async Task A_cached_list_command_and_query_store_is_registered_under_its_name()
        {
            await environment.Provider.WriteTextAsync("people.json", PeopleJson);

            environment.Services.CreateAndAddDataStoreListCommandAndQueryJsonWithCache<string, PersonEntity>("people", JsonStoreEnvironment.StorageName, "people.json", NoRefresh);

            var registered = Track(environment.ListFactory.GetDataStoreListCommandAndQuery<string, PersonEntity>("people"));
            Assert.IsType<DataStoreListCommandAndQueryCache<string, PersonEntity>>(registered);
            Assert.Equal("Lovelace", (await registered.GetByIdAsync("1"))?.Name);
        }

        [Fact]
        public async Task A_cached_list_load_store_answers_from_the_file()
        {
            await environment.Provider.WriteTextAsync("people.json", PeopleJson);

            var cache = Track(environment.Services.CreateDataStoreListLoadJsonWithCache<PersonEntity>("people", JsonStoreEnvironment.StorageName, "people.json", NoRefresh));

            Assert.Equal(["Turing", "Lovelace"], (await cache.GetAllAsync()).Select(p => p.Name));
        }

        [Fact]
        public async Task A_cached_list_load_store_is_registered_under_its_name()
        {
            await environment.Provider.WriteTextAsync("people.json", PeopleJson);

            environment.Services.CreateAndAddDataStoreListLoadJsonWithCache<PersonEntity>("people", JsonStoreEnvironment.StorageName, "people.json", NoRefresh);

            var registered = Track(environment.ListFactory.GetDataStoreListLoad<PersonEntity>("people"));
            Assert.Equal(2, (await registered.GetAllAsync()).Count);
        }

        [Fact]
        public async Task The_list_helpers_write_and_read_a_whole_list()
        {
            await environment.Provider.WriteListValuesAsync("people.json", [new PersonEntity { Id = "1", Name = "Lovelace" }], NullLogger.Instance);

            var read = await environment.Provider.GetListValuesAsync<PersonEntity>("people.json", NullLogger.Instance);

            Assert.Equal("Lovelace", Assert.Single(read).Name);
        }

        [Fact]
        public async Task The_list_helpers_report_what_they_did_at_trace_level_only_when_it_is_enabled()
        {
            var logger = new TraceLogger();
            await environment.Provider.WriteListValuesAsync("people.json", [new PersonEntity { Id = "1", Name = "Lovelace" }], logger);
            await environment.Provider.GetListValuesAsync<PersonEntity>("people.json", logger);

            Assert.Equal(2, logger.Messages.Count);
            Assert.Contains("WriteListValuesAsync<people.json>() -> 1", logger.Messages[0], StringComparison.Ordinal);
            Assert.Contains("GetListValues<people.json>() -> 1", logger.Messages[1], StringComparison.Ordinal);

            var quiet = new TraceLogger(enabled: false);
            await environment.Provider.GetListValuesAsync<PersonEntity>("people.json", quiet);
            Assert.Empty(quiet.Messages);
        }

        [Fact]
        public async Task A_dictionary_store_is_built_over_the_named_file()
        {
            await environment.Provider.WriteTextAsync("people.json", """{"ada":"Lovelace","alan":"Turing"}""");

            var store = environment.Services.CreateDataStoreDictionaryJson<string, string>(JsonStoreEnvironment.StorageName, "people.json");

            Assert.Equal("Turing", await store.GetByIdAsync("alan"));
        }

        [Fact]
        public async Task A_dictionary_store_is_registered_under_its_name()
        {
            await environment.Provider.WriteTextAsync("people.json", """{"ada":"Lovelace"}""");

            environment.Services.CreateAndAddDataStoreDictionaryJson<string, string>("people", JsonStoreEnvironment.StorageName, "people.json");

            var registered = environment.DictionaryFactory.GetDataStoreDictionaryCommandAndQuery<string, string>("people");
            Assert.IsType<DataStoreDictionaryCommandAndQueryJson<string, string>>(registered);
            Assert.Equal("Lovelace", await registered.GetByIdAsync("ada"));
        }

        [Fact]
        public async Task A_cached_dictionary_store_answers_from_the_file_and_writes_through()
        {
            await environment.Provider.WriteTextAsync("people.json", """{"ada":"Lovelace"}""");

            var cache = Track(environment.Services.CreateDataStoreDictionaryJsonWithCache<string, string>("people", JsonStoreEnvironment.StorageName, "people.json", NoRefresh));

            Assert.Equal("Lovelace", await cache.GetByIdAsync("ada"));

            await cache.InsertAsync("alan", "Turing");

            Assert.Contains("Turing", await environment.Provider.ReadTextAsync("people.json"));
            Assert.Equal("Turing", await cache.GetByIdAsync("alan"));
        }

        [Fact]
        public async Task A_cached_dictionary_store_is_registered_under_its_name()
        {
            await environment.Provider.WriteTextAsync("people.json", """{"ada":"Lovelace"}""");

            environment.Services.CreateAndAddDataStoreDictionaryJsonWithCache<string, string>("people", JsonStoreEnvironment.StorageName, "people.json", NoRefresh);

            var registered = Track(environment.DictionaryFactory.GetDataStoreDictionaryCommandAndQuery<string, string>("people"));
            Assert.IsType<DataStoreDictionaryCommandAndQueryCache<string, string>>(registered);
            Assert.Equal("Lovelace", await registered.GetByIdAsync("ada"));
        }

        [Fact]
        public async Task The_dictionary_helper_reads_a_whole_dictionary_and_logs_at_trace_only_when_enabled()
        {
            await environment.Provider.WriteTextAsync("people.json", """{"ada":"Lovelace","alan":"Turing"}""");
            var logger = new TraceLogger();

            var read = await environment.Provider.GetDictionaryValuesAsync<string, string>("people.json", logger);

            Assert.Equal(2, read.Count);
            Assert.Contains("GetDictionaryValues<people.json>() -> 2", Assert.Single(logger.Messages), StringComparison.Ordinal);

            var quiet = new TraceLogger(enabled: false);
            await environment.Provider.GetDictionaryValuesAsync<string, string>("people.json", quiet);
            Assert.Empty(quiet.Messages);
        }

        public sealed class PersonEntity : Entity<string>
        {
            public string Name { get; set; } = "";
        }

        private sealed class TraceLogger(bool enabled = true) : ILogger
        {
            public List<string> Messages { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => enabled && logLevel == LogLevel.Trace;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Messages.Add(formatter(state, exception));
        }
    }
}

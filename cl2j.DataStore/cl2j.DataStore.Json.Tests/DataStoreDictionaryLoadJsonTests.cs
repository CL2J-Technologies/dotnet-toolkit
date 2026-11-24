using cl2j.DataStore.Json.Dictionary;
using cl2j.FileStorage.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace cl2j.DataStore.Json.Tests
{
    public sealed class DataStoreDictionaryLoadJsonTests : IDisposable
    {
        private readonly JsonStoreEnvironment environment = new();

        public void Dispose() => environment.Dispose();

        [Fact]
        public async Task Every_entry_in_the_file_is_loaded_under_its_key()
        {
            await environment.Provider.WriteTextAsync("people.json", """{"2":{"id":"2","name":"Turing"},"1":{"id":"1","name":"Lovelace"}}""");
            var load = new DataStoreDictionaryLoadJson<string, Person>(environment.Provider, "people.json", NullLogger.Instance);

            var all = await load.GetAllAsync();

            Assert.Equal(2, all.Count);
            Assert.Equal("Turing", all["2"].Name);
            Assert.Equal("Lovelace", all["1"].Name);
        }

        [Fact]
        public async Task A_file_that_is_not_there_loads_as_empty()
        {
            var load = new DataStoreDictionaryLoadJson<string, Person>(environment.Provider, "missing.json", NullLogger.Instance);

            Assert.Empty(await load.GetAllAsync());
        }
    }
}

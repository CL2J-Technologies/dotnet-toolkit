using cl2j.DataStore.Json.List;
using cl2j.FileStorage.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace cl2j.DataStore.Json.Tests
{
    public sealed class DataStoreListLoadJsonTests : IDisposable
    {
        private readonly JsonStoreEnvironment environment = new();

        public void Dispose() => environment.Dispose();

        [Fact]
        public async Task Everything_in_the_file_is_loaded_in_order()
        {
            await environment.Provider.WriteTextAsync("people.json", """[{"id":"2","name":"Turing"},{"id":"1","name":"Lovelace"}]""");
            var load = new DataStoreListLoadJson<Person>(environment.Provider, "people.json", NullLogger.Instance);

            var all = await load.GetAllAsync();

            Assert.Equal(["Turing", "Lovelace"], all.Select(p => p.Name));
            Assert.Equal(["2", "1"], all.Select(p => p.Id));
        }

        [Fact]
        public async Task A_file_that_is_not_there_loads_as_empty()
        {
            var load = new DataStoreListLoadJson<Person>(environment.Provider, "missing.json", NullLogger.Instance);

            Assert.Empty(await load.GetAllAsync());
        }
    }
}

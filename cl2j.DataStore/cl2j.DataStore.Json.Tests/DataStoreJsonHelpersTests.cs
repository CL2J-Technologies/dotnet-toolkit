using cl2j.FileStorage.Extensions;
using Xunit;

namespace cl2j.DataStore.Json.Tests
{
    public sealed class DataStoreJsonHelpersTests : IDisposable
    {
        private const string FileName = "people.json";

        private readonly JsonStoreEnvironment environment = new();

        public void Dispose() => environment.Dispose();

        [Fact]
        public async Task GetOrFetch_reads_the_file_when_it_exists_and_does_not_fetch()
        {
            await environment.Provider.WriteTextAsync(FileName, "[\"on disk\"]");
            var fetched = false;

            var result = await environment.Provider.GetOrFetch(FileName, () =>
            {
                fetched = true;
                return Task.FromResult(new List<string> { "fetched" });
            });

            Assert.Equal(["on disk"], result);
            Assert.False(fetched);
        }

        [Fact]
        public async Task GetOrFetch_fetches_and_writes_the_file_when_it_is_missing()
        {
            var result = await environment.Provider.GetOrFetch(FileName, () => Task.FromResult(new List<string> { "fetched" }));

            Assert.Equal(["fetched"], result);
            Assert.True(await environment.Provider.ExistsAsync(FileName));
            Assert.Contains("fetched", await environment.Provider.ReadTextAsync(FileName));
        }

        [Fact]
        public async Task GetOrFetch_does_not_fetch_a_second_time_once_the_file_is_written()
        {
            var fetches = 0;
            Task<List<string>> Fetch()
            {
                fetches++;
                return Task.FromResult(new List<string> { "fetched" });
            }

            await environment.Provider.GetOrFetch(FileName, Fetch);
            var second = await environment.Provider.GetOrFetch(FileName, Fetch);

            Assert.Equal(1, fetches);
            Assert.Equal(["fetched"], second);
        }

        [Fact]
        public async Task TryRead_reads_the_file_when_it_exists()
        {
            await environment.Provider.WriteTextAsync(FileName, "[\"on disk\"]");

            var result = await environment.Provider.TryRead(FileName, new List<string> { "default" });

            Assert.Equal(["on disk"], result);
        }

        [Fact]
        public async Task TryRead_answers_the_default_when_the_file_is_missing_and_does_not_create_it()
        {
            var defaultValue = new List<string> { "default" };

            var result = await environment.Provider.TryRead(FileName, defaultValue);

            Assert.Same(defaultValue, result);
            Assert.False(await environment.Provider.ExistsAsync(FileName));
        }
    }
}

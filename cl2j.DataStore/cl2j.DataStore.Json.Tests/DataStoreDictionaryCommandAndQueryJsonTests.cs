using cl2j.DataStore.Json.Dictionary;
using cl2j.FileStorage.Core;
using cl2j.FileStorage.Provider.Disk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace cl2j.DataStore.Json.Tests
{
    /// <summary>
    ///     Four of the six operations on this store threw <see cref="NotImplementedException"/>.
    ///     The type satisfied its interface at compile time, so a consumer wiring it through DI got
    ///     something that resolved, built and started, and then threw the first time anyone read a
    ///     single entry or wrote one. See issue #10.
    ///
    ///     <para>
    ///     It is also the store the cache decorator is written to wrap:
    ///     <c>DataStoreDictionaryCommandAndQueryCache</c> delegates every write to an inner
    ///     <c>IDataStoreDictionaryCommandAndQuery</c>, and this is the only one there is.
    ///     </para>
    ///
    ///     <para>
    ///     These tests use the real disk provider against a temporary directory rather than a fake,
    ///     so the JSON round trip is exercised too.
    ///     </para>
    /// </summary>
    public sealed class DataStoreDictionaryCommandAndQueryJsonTests : IDisposable
    {
        private const string FileName = "people.json";

        private readonly string root;
        private readonly DataStoreDictionaryCommandAndQueryJson<string, string> store;

        public DataStoreDictionaryCommandAndQueryJsonTests()
        {
            root = Path.Combine(Path.GetTempPath(), "cl2j-datastore-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Disk:Path"] = root })
                .Build();

            var provider = new FileStorageProviderDisk();
            provider.Initialize("Disk", configuration.GetSection("Disk"));

            store = new DataStoreDictionaryCommandAndQueryJson<string, string>(provider, FileName, NullLogger.Instance);
        }

        public void Dispose()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        private async Task SeedAsync(params (string Key, string Value)[] items)
            => await store.ReplaceAllByAsync(items.ToDictionary(i => i.Key, i => i.Value));

        [Fact]
        public async Task An_entry_can_be_read_back_by_its_key()
        {
            await SeedAsync(("ada", "Lovelace"), ("alan", "Turing"));

            Assert.Equal("Turing", await store.GetByIdAsync("alan"));
        }

        [Fact]
        public async Task A_key_that_is_not_there_reads_as_default()
        {
            await SeedAsync(("ada", "Lovelace"));

            Assert.Null(await store.GetByIdAsync("nobody"));
        }

        [Fact]
        public async Task An_inserted_entry_joins_the_others()
        {
            await SeedAsync(("ada", "Lovelace"));

            await store.InsertAsync("alan", "Turing");

            var all = await store.GetAllAsync();
            Assert.Equal(2, all.Count);
            Assert.Equal("Turing", all["alan"]);
            Assert.Equal("Lovelace", all["ada"]);
        }

        [Fact]
        public async Task Inserting_a_key_that_is_already_there_is_refused()
        {
            //The interface says so — "Insert a new item. The key must not exists" — and the cache
            //decorator uses Dictionary.Add, which throws on a duplicate.
            await SeedAsync(("ada", "Lovelace"));

            await Assert.ThrowsAsync<ArgumentException>(() => store.InsertAsync("ada", "Someone else"));
        }

        [Fact]
        public async Task A_refused_insert_leaves_the_file_as_it_was()
        {
            //The check has to happen before the write, not after.
            await SeedAsync(("ada", "Lovelace"));

            await Assert.ThrowsAsync<ArgumentException>(() => store.InsertAsync("ada", "Someone else"));

            var all = await store.GetAllAsync();
            Assert.Equal("Lovelace", Assert.Single(all).Value);
        }

        [Fact]
        public async Task An_updated_entry_keeps_its_key_and_changes_its_value()
        {
            await SeedAsync(("ada", "Lovelace"), ("alan", "Turing"));

            await store.UpdateAsync("ada", "Byron");

            var all = await store.GetAllAsync();
            Assert.Equal("Byron", all["ada"]);
            Assert.Equal("Turing", all["alan"]);
        }

        [Fact]
        public async Task Updating_a_key_that_is_not_there_is_refused()
        {
            //Insert requires the key to be absent, so Update requiring it to be present is the
            //symmetric half. Silently doing nothing would make a typo look like a success.
            await SeedAsync(("ada", "Lovelace"));

            await Assert.ThrowsAsync<KeyNotFoundException>(() => store.UpdateAsync("nobody", "Anyone"));
        }

        [Fact]
        public async Task A_deleted_entry_is_gone_and_the_others_stay()
        {
            await SeedAsync(("ada", "Lovelace"), ("alan", "Turing"));

            await store.DeleteAsync("ada");

            var all = await store.GetAllAsync();
            Assert.Equal("Turing", Assert.Single(all).Value);
        }

        [Fact]
        public async Task Deleting_a_key_that_is_not_there_is_not_an_error()
        {
            //The cache decorator does `if (cache.Remove(key))`, which tolerates absence, so the
            //store it delegates to must tolerate it too.
            await SeedAsync(("ada", "Lovelace"));

            await store.DeleteAsync("nobody");

            Assert.Single(await store.GetAllAsync());
        }

        [Fact]
        public async Task Writes_land_one_after_another_rather_than_on_top_of_each_other()
        {
            //Each write is a read, a change and a write of the whole file. Without the lock around
            //all three, two concurrent inserts both read the same file and the second overwrites
            //the first.
            await SeedAsync(("seed", "value"));

            await Task.WhenAll(Enumerable.Range(0, 20).Select(i => store.InsertAsync($"key-{i:D2}", $"value-{i}")));

            var all = await store.GetAllAsync();
            Assert.Equal(21, all.Count);
        }
    }
}

using cl2j.DataStore.Json.List;
using cl2j.FileStorage.Extensions;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace cl2j.DataStore.Json.Tests
{
    /// <summary>
    ///     The list twin of <see cref="DataStoreDictionaryCommandAndQueryJsonTests"/>: the same
    ///     real disk provider against a temporary directory, so the JSON round trip is exercised.
    /// </summary>
    public sealed class DataStoreListCommandAndQueryJsonTests : IDisposable
    {
        private const string FileName = "people.json";

        private readonly JsonStoreEnvironment environment = new();
        private readonly DataStoreListCommandAndQueryJson<string, Person> store;

        public DataStoreListCommandAndQueryJsonTests()
        {
            store = new DataStoreListCommandAndQueryJson<string, Person>(environment.Provider, FileName, p => p.Id, NullLogger.Instance);
        }

        public void Dispose() => environment.Dispose();

        private static Person P(string id, string name) => new() { Id = id, Name = name };

        private Task SeedAsync(params Person[] people) => store.ReplaceAllByAsync(people);

        [Fact]
        public async Task A_store_with_no_file_reads_as_empty()
        {
            Assert.Empty(await store.GetAllAsync());
            Assert.Null(await store.GetByIdAsync("ada"));
        }

        [Fact]
        public async Task What_was_replaced_in_is_read_back_in_order()
        {
            await SeedAsync(P("2", "Turing"), P("1", "Lovelace"));

            var all = await store.GetAllAsync();

            Assert.Equal(["2", "1"], all.Select(p => p.Id));
            Assert.Equal(["Turing", "Lovelace"], all.Select(p => p.Name));
        }

        [Fact]
        public async Task Replacing_everything_drops_what_was_there()
        {
            await SeedAsync(P("1", "Lovelace"), P("2", "Turing"));

            await SeedAsync(P("3", "Hopper"));

            Assert.Equal("Hopper", Assert.Single(await store.GetAllAsync()).Name);
        }

        [Fact]
        public async Task An_entry_can_be_read_back_by_its_key()
        {
            await SeedAsync(P("1", "Lovelace"), P("2", "Turing"));

            Assert.Equal("Turing", (await store.GetByIdAsync("2"))?.Name);
        }

        [Fact]
        public async Task A_key_that_is_not_there_reads_as_default()
        {
            await SeedAsync(P("1", "Lovelace"));

            Assert.Null(await store.GetByIdAsync("nobody"));
        }

        [Fact]
        public async Task An_inserted_entry_joins_the_others()
        {
            await SeedAsync(P("1", "Lovelace"));

            await store.InsertAsync(P("2", "Turing"));

            var all = await store.GetAllAsync();
            Assert.Equal(["1", "2"], all.Select(p => p.Id));
        }

        [Fact]
        public async Task An_updated_entry_keeps_its_place_and_changes_its_value()
        {
            await SeedAsync(P("1", "Lovelace"), P("2", "Turing"));

            await store.UpdateAsync(P("1", "Byron"));

            var all = await store.GetAllAsync();
            Assert.Equal(["Byron", "Turing"], all.Select(p => p.Name));
        }

        [Fact]
        public async Task A_deleted_entry_is_gone_and_the_others_stay()
        {
            await SeedAsync(P("1", "Lovelace"), P("2", "Turing"));

            await store.DeleteAsync("1");

            Assert.Equal("Turing", Assert.Single(await store.GetAllAsync()).Name);
        }

        [Fact]
        public async Task The_key_comes_from_the_predicate_not_from_a_property_called_Id()
        {
            var byName = new DataStoreListCommandAndQueryJson<string, Person>(environment.Provider, "by-name.json", p => p.Name, NullLogger.Instance);
            await byName.ReplaceAllByAsync([P("1", "Lovelace"), P("2", "Turing")]);

            await byName.UpdateAsync(P("9", "Turing"));

            Assert.Equal("9", (await byName.GetByIdAsync("Turing"))?.Id);
            Assert.Equal(2, (await byName.GetAllAsync()).Count);
        }

        [Fact]
        public async Task Indented_output_is_opt_in()
        {
            var indented = new DataStoreListCommandAndQueryJson<string, Person>(environment.Provider, "indented.json", p => p.Id, NullLogger.Instance, indent: true);

            await indented.ReplaceAllByAsync([P("1", "Lovelace")]);
            await SeedAsync(P("1", "Lovelace"));

            Assert.Contains('\n', await environment.Provider.ReadTextAsync("indented.json"));
            Assert.DoesNotContain('\n', await environment.Provider.ReadTextAsync(FileName));
        }

        [Fact]
        public async Task Writes_land_one_after_another_rather_than_on_top_of_each_other()
        {
            await SeedAsync(P("seed", "value"));

            await Task.WhenAll(Enumerable.Range(0, 20).Select(i => store.InsertAsync(P($"key-{i:D2}", $"value-{i}"))));

            Assert.Equal(21, (await store.GetAllAsync()).Count);
        }

        [Fact]
        public async Task A_duplicate_insert_leaves_the_file_as_it_was()
        {
            await SeedAsync(P("1", "Lovelace"));

            await Assert.ThrowsAnyAsync<Exception>(() => store.InsertAsync(P("1", "Someone else")));

            Assert.Equal("Lovelace", Assert.Single(await store.GetAllAsync()).Name);
        }

        [Fact]
        public async Task An_update_of_a_missing_key_leaves_the_file_as_it_was()
        {
            await SeedAsync(P("1", "Lovelace"));

            await Assert.ThrowsAnyAsync<Exception>(() => store.UpdateAsync(P("nobody", "Anyone")));

            Assert.Equal("Lovelace", Assert.Single(await store.GetAllAsync()).Name);
        }

        /// <summary>
        ///     Not an error, as in the dictionary store: the store ends up as the caller asked, and
        ///     the cache decorator relies on it to drop an entry the file no longer holds. Throwing
        ///     here would leave that entry in the cache, with no way to remove it until a refresh.
        /// </summary>
        [Fact]
        public async Task A_delete_of_a_missing_key_is_not_an_error_and_leaves_the_file_as_it_was()
        {
            await SeedAsync(P("1", "Lovelace"));

            await store.DeleteAsync("nobody");

            Assert.Equal("Lovelace", Assert.Single(await store.GetAllAsync()).Name);
        }

        /// <summary>
        ///     A file name under a "directory" that is in fact a file: reading finds nothing, and
        ///     writing fails. Every write path used to catch that and log it at Trace, so a write
        ///     that never reached the disk looked exactly like one that did.
        /// </summary>
        private async Task<DataStoreListCommandAndQueryJson<string, Person>> UnwritableStoreAsync()
        {
            await environment.Provider.WriteTextAsync("blocker", "not a directory");
            return new DataStoreListCommandAndQueryJson<string, Person>(environment.Provider, "blocker/people.json", p => p.Id, NullLogger.Instance);
        }

        [Fact]
        public async Task An_insert_whose_write_fails_is_reported()
        {
            var unwritable = await UnwritableStoreAsync();

            await Assert.ThrowsAnyAsync<Exception>(() => unwritable.InsertAsync(P("1", "Lovelace")));
        }

        [Fact]
        public async Task Inserting_a_key_that_is_already_there_is_refused()
        {
            await SeedAsync(P("1", "Lovelace"));

            await Assert.ThrowsAsync<ConflictException>(() => store.InsertAsync(P("1", "Someone else")));
        }

        [Fact]
        public async Task Updating_a_key_that_is_not_there_is_refused()
        {
            await SeedAsync(P("1", "Lovelace"));

            await Assert.ThrowsAsync<NotFoundException>(() => store.UpdateAsync(P("nobody", "Anyone")));
        }
    }
}

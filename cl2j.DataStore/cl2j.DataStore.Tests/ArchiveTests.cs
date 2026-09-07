using cl2j.DataStore.List;
using Xunit;

namespace cl2j.DataStore.Tests
{
    /// <summary>
    ///     The decorator that writes a snapshot of the whole store to file storage after every
    ///     change. Its entire value is that the snapshots are distinguishable afterwards, so the
    ///     name it chooses is the thing worth testing.
    /// </summary>
    public class ArchiveTests
    {
        private static readonly DateTimeOffset Afternoon = new(2026, 9, 7, 13, 45, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Morning = new(2026, 9, 7, 1, 45, 0, TimeSpan.Zero);

        private static (DataStoreListCommandAndQueryArchive<string, Person> Archive, InMemoryListStore<string, Person> Store, InMemoryFileStorage Storage)
            Build(DateTimeOffset now, params Person[] seed)
        {
            var store = new InMemoryListStore<string, Person>(p => p.Id);
            store.Seed(seed);
            var storage = new InMemoryFileStorage();
            var archive = new DataStoreListCommandAndQueryArchive<string, Person>(
                store, storage, "people-{0}.json", () => now);
            return (archive, store, storage);
        }

        [Fact]
        public async Task A_change_reaches_the_store_underneath()
        {
            var (archive, store, _) = Build(Afternoon);

            await archive.InsertAsync(new Person("1", "Renée"));

            Assert.Equal("Renée", Assert.Single(store.Contents).Name);
        }

        [Fact]
        public async Task A_change_is_archived_under_the_hour_it_happened()
        {
            //The bug this test was written for: the format string said "hh", which is the
            //twelve-hour clock, so 13:45 and 01:45 produced the same name and the afternoon
            //snapshot overwrote the morning one. Half a day of archives, silently.
            var (archive, _, storage) = Build(Afternoon);

            await archive.InsertAsync(new Person("1", "Renée"));

            Assert.Equal("people-20260907-1345.json", Assert.Single(storage.Files).Key);
        }

        [Fact]
        public async Task Morning_and_afternoon_do_not_share_a_name()
        {
            var storage = new InMemoryFileStorage();
            var store = new InMemoryListStore<string, Person>(p => p.Id);

            var atNight = new DataStoreListCommandAndQueryArchive<string, Person>(store, storage, "people-{0}.json", () => Morning);
            await atNight.InsertAsync(new Person("1", "Renée"));

            var atNoon = new DataStoreListCommandAndQueryArchive<string, Person>(store, storage, "people-{0}.json", () => Afternoon);
            await atNoon.InsertAsync(new Person("2", "Ari"));

            Assert.Equal(2, storage.Files.Count);
        }

        [Fact]
        public async Task The_archive_holds_the_state_after_the_change()
        {
            var (archive, _, storage) = Build(Afternoon, new Person("1", "Renée"));

            await archive.InsertAsync(new Person("2", "Ari"));

            var written = Assert.Single(storage.Files).Value;
            Assert.Contains("Renée", written, StringComparison.Ordinal);
            Assert.Contains("Ari", written, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Every_kind_of_change_is_archived()
        {
            var (archive, _, storage) = Build(Afternoon, new Person("1", "Renée"));

            await archive.InsertAsync(new Person("2", "Ari"));
            await archive.UpdateAsync(new Person("2", "Ari Tremblay"));
            await archive.DeleteAsync("2");
            await archive.ReplaceAllByAsync([new Person("3", "Cléo")]);

            //One name, because they land in the same minute — so four writes, one file.
            Assert.Equal(4, storage.WritesInOrder.Count);
            Assert.Single(storage.Files);
            Assert.Contains("Cléo", storage.Files.Single().Value, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Reading_is_not_archived()
        {
            var (archive, _, storage) = Build(Afternoon, new Person("1", "Renée"));

            await archive.GetAllAsync();
            await archive.GetByIdAsync("1");

            Assert.Empty(storage.Files);
        }

        [Fact]
        public async Task Reads_pass_straight_through()
        {
            var (archive, _, _) = Build(Afternoon, new Person("1", "Renée"));

            Assert.Equal("Renée", Assert.Single(await archive.GetAllAsync()).Name);
            Assert.Equal("Renée", (await archive.GetByIdAsync("1"))?.Name);
        }

        [Fact]
        public async Task Each_change_costs_a_full_read_of_the_store()
        {
            //Characterisation, and the price of the design: the snapshot is built by asking the
            //store for everything it has, so a store with a million rows reads a million rows per
            //write.
            var (archive, store, _) = Build(Afternoon);

            await archive.InsertAsync(new Person("1", "Renée"));

            Assert.Equal(["Insert", "GetAll"], store.Calls);
        }

        [Fact]
        public async Task An_archive_that_cannot_be_written_fails_the_change_that_already_happened()
        {
            //Characterisation. The archive is written after the store has accepted the change, and
            //a failure to write it is not caught — so the caller sees an exception for an operation
            //that did succeed, and a retry would apply it twice.
            var (archive, store, storage) = Build(Afternoon);
            storage.FailWrites = true;

            await Assert.ThrowsAsync<IOException>(() => archive.InsertAsync(new Person("1", "Renée")));

            Assert.Single(store.Contents);
        }
    }
}

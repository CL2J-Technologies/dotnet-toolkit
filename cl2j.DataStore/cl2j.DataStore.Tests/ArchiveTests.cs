using cl2j.DataStore.List;
using Microsoft.Extensions.Logging;
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

        private static (DataStoreListCommandAndQueryArchive<string, Person> Archive, InMemoryListStore<string, Person> Store, InMemoryFileStorage Storage, RecordingLogger Logger)
            BuildLogged(DateTimeOffset now, params Person[] seed)
        {
            var store = new InMemoryListStore<string, Person>(p => p.Id);
            store.Seed(seed);
            var storage = new InMemoryFileStorage();
            var logger = new RecordingLogger();
            var archive = new DataStoreListCommandAndQueryArchive<string, Person>(
                store, storage, "people-{0}.json", () => now, logger);
            return (archive, store, storage, logger);
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
            //Characterisation, and the price of the design, kept on purpose: the archive decorates
            //any store, knows no key and sees no change made behind its back, so the only way to
            //write the state after the change is to ask the store for everything it has. A store
            //with a million rows reads a million rows per write.
            var (archive, store, _) = Build(Afternoon);

            await archive.InsertAsync(new Person("1", "Renée"));

            Assert.Equal(["Insert", "GetAll"], store.Calls);
        }

        [Fact]
        public async Task An_archive_that_cannot_be_written_does_not_fail_the_change_that_already_happened()
        {
            //The archive is written after the store has accepted the change. Throwing for it told
            //the caller that an operation which succeeded had failed, and a retry applied it twice.
            //The failure is logged at Error instead.
            var (archive, store, storage, logger) = BuildLogged(Afternoon);
            storage.FailWrites = true;

            await archive.InsertAsync(new Person("1", "Renée"));

            Assert.Single(store.Contents);
            Assert.Empty(storage.Files);
            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Error, entry.Level);
            Assert.IsType<IOException>(entry.Exception);
            Assert.Contains("people-20260907-1345.json", entry.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Every_kind_of_change_survives_an_archive_that_cannot_be_written()
        {
            var (archive, store, storage, logger) = BuildLogged(Afternoon, new Person("1", "Renée"));
            storage.FailWrites = true;

            await archive.InsertAsync(new Person("2", "Ari"));
            await archive.UpdateAsync(new Person("2", "Ari Tremblay"));
            await archive.DeleteAsync("1");
            await archive.ReplaceAllByAsync([new Person("3", "Cléo")]);

            Assert.Equal("Cléo", Assert.Single(store.Contents).Name);
            Assert.Equal(4, logger.Entries.Count(e => e.Level == LogLevel.Error));
        }

        [Fact]
        public async Task An_archive_that_cannot_be_written_does_not_fail_the_change_when_no_logger_was_given()
        {
            //Without a logger there is nowhere to report to, but the change must still go through.
            var (archive, store, storage) = Build(Afternoon);
            storage.FailWrites = true;

            await archive.InsertAsync(new Person("1", "Renée"));

            Assert.Single(store.Contents);
        }

        [Fact]
        public async Task An_archive_that_is_written_logs_no_error()
        {
            var (archive, _, _, logger) = BuildLogged(Afternoon);

            await archive.InsertAsync(new Person("1", "Renée"));

            Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
        }

        [Fact]
        public async Task A_store_that_cannot_be_read_back_does_not_fail_the_change_either()
        {
            //The snapshot is built from the store after the change, so reading it is part of the
            //archive too, and fails the same way.
            var store = new InMemoryListStore<string, Person>(p => p.Id);
            var logger = new RecordingLogger();
            var archive = new DataStoreListCommandAndQueryArchive<string, Person>(
                new UnreadableAfterWriteStore(store), new InMemoryFileStorage(), "people-{0}.json", () => Afternoon, logger);

            await archive.InsertAsync(new Person("1", "Renée"));

            Assert.Single(store.Contents);
            Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level);
        }

        public class Pet
        {
            public string Id { get; set; } = "";
        }

        public sealed class Cat : Pet
        {
            public int Lives { get; set; }
        }

        [Fact]
        public async Task A_snapshot_of_a_polymorphic_entity_is_refused_by_default_and_logged_without_failing_the_change()
        {
            var store = new InMemoryListStore<string, Pet>(p => p.Id);
            var storage = new InMemoryFileStorage();
            var logger = new RecordingLogger();
            var archive = new DataStoreListCommandAndQueryArchive<string, Pet>(store, storage, "pets-{0}.json", () => Afternoon, logger);

            await archive.InsertAsync(new Cat { Id = "1", Lives = 9 });

            Assert.Single(store.Contents);
            Assert.Empty(storage.Files);
            Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level);
        }

        [Fact]
        public async Task A_snapshot_of_a_polymorphic_entity_is_written_when_its_type_is_listed()
        {
            var store = new InMemoryListStore<string, Pet>(p => p.Id);
            var storage = new InMemoryFileStorage();
            var logger = new RecordingLogger();
            var archive = new DataStoreListCommandAndQueryArchive<string, Pet>(
                store, storage, "pets-{0}.json", () => Afternoon, logger,
                new FileStorage.Extensions.JsonStorageOptions { AllowedTypes = [typeof(Cat)] });

            await archive.InsertAsync(new Cat { Id = "1", Lives = 9 });

            Assert.Contains("\"lives\":9", Assert.Single(storage.Files).Value, StringComparison.Ordinal);
            Assert.Empty(logger.Entries);
        }

        private sealed class UnreadableAfterWriteStore(InMemoryListStore<string, Person> inner) : IDataStoreListCommandAndQuery<string, Person>
        {
            public Task<IReadOnlyList<Person>> GetAllAsync() => throw new InvalidOperationException("the store cannot be read");
            public Task<Person?> GetByIdAsync(string key) => inner.GetByIdAsync(key);
            public Task InsertAsync(Person entity) => inner.InsertAsync(entity);
            public Task UpdateAsync(Person entity) => inner.UpdateAsync(entity);
            public Task DeleteAsync(string key) => inner.DeleteAsync(key);
            public Task ReplaceAllByAsync(ICollection<Person> items) => inner.ReplaceAllByAsync(items);
        }
    }
}

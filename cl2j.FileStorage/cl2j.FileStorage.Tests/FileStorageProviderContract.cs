using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using Xunit;

namespace cl2j.FileStorage.Tests
{
    /// <summary>
    ///     What every <see cref="IFileStorageProvider"/> has to do, regardless of where it puts the
    ///     bytes. A provider is tested by deriving from this and saying which one it is; the tests
    ///     come with it, so a second implementation cannot quietly satisfy fewer of them.
    ///
    ///     <para>
    ///     The shape is inherited from the tests this replaces, which had the same intent expressed
    ///     as a helper class each runner forwarded to by hand. Deriving means a test added here
    ///     runs for every provider without anyone remembering to forward it.
    ///     </para>
    /// </summary>
    public abstract class FileStorageProviderContract
    {
        protected abstract IFileStorageProvider Provider { get; }

        /// <summary>
        ///     A directory of its own per test, so nothing depends on what another test left
        ///     behind. The tests this replaces shared one directory and cleared it on the way in,
        ///     which only works while they run one at a time.
        /// </summary>
        private static string Folder(string test) => $"contract-{test}";

        private static string File(string test, string name = "file.txt") => $"{Folder(test)}/{name}";

        [Fact]
        public async Task What_was_written_is_what_is_read_back()
        {
            var file = File(nameof(What_was_written_is_what_is_read_back));

            await Provider.WriteTextAsync(file, "hello");

            Assert.True(await Provider.ExistsAsync(file));
            Assert.Equal("hello", await Provider.ReadTextAsync(file));
        }

        [Fact]
        public async Task Writing_over_a_file_replaces_it_rather_than_adding_to_it()
        {
            var file = File(nameof(Writing_over_a_file_replaces_it_rather_than_adding_to_it));

            await Provider.WriteTextAsync(file, "the first, which is longer");
            await Provider.WriteTextAsync(file, "second");

            Assert.Equal("second", await Provider.ReadTextAsync(file));
        }

        [Fact]
        public async Task Appending_adds_to_what_is_already_there()
        {
            var file = File(nameof(Appending_adds_to_what_is_already_there));

            await Provider.AppendTextAsync(file, "first");
            await Provider.AppendTextAsync(file, "second");

            Assert.Equal("firstsecond", await Provider.ReadTextAsync(file));
        }

        [Fact]
        public async Task Appending_to_a_file_that_is_not_there_creates_it()
        {
            var file = File(nameof(Appending_to_a_file_that_is_not_there_creates_it));

            await Provider.AppendTextAsync(file, "from nothing");

            Assert.Equal("from nothing", await Provider.ReadTextAsync(file));
        }

        [Fact]
        public async Task A_listing_names_the_files_that_are_there()
        {
            var folder = Folder(nameof(A_listing_names_the_files_that_are_there));

            await Provider.WriteTextAsync($"{folder}/one.txt", "1");
            await Provider.WriteTextAsync($"{folder}/two.txt", "2");

            var files = await Provider.ListFilesAsync(folder);

            Assert.Equal(2, files.Count());
        }

        [Fact]
        public async Task A_deleted_file_is_gone()
        {
            var file = File(nameof(A_deleted_file_is_gone));
            await Provider.WriteTextAsync(file, "doomed");

            await Provider.DeleteAsync(file);

            Assert.False(await Provider.ExistsAsync(file));
        }

        [Fact]
        public async Task A_file_that_was_never_written_does_not_exist()
        {
            Assert.False(await Provider.ExistsAsync(File(nameof(A_file_that_was_never_written_does_not_exist), "never-written.txt")));
        }

        [Fact]
        public async Task Writing_into_a_folder_that_is_not_there_creates_it()
        {
            var file = $"{Folder(nameof(Writing_into_a_folder_that_is_not_there_creates_it))}/deeper/still/file.txt";

            await Provider.WriteTextAsync(file, "nested");

            Assert.Equal("nested", await Provider.ReadTextAsync(file));
        }
    }
}

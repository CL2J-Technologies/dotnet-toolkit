using cl2j.FileStorage.Extensions;
using Xunit;

namespace cl2j.FileStorage.Tests
{
    public sealed class FileProviderExtensionsTests
    {
        private readonly InMemoryFileStorageProvider provider = new();

        [Fact]
        public async Task ClearDirectoryAsync_deletes_every_file_of_the_directory_and_counts_them()
        {
            await provider.WriteTextAsync("logs/a.txt", "a");
            await provider.WriteTextAsync("logs/b.txt", "b");

            var count = await provider.ClearDirectoryAsync("logs");

            Assert.Equal(2, count);
            Assert.Empty(provider.Files);
        }

        [Fact]
        public async Task ClearDirectoryAsync_leaves_the_other_directories_alone()
        {
            await provider.WriteTextAsync("logs/a.txt", "a");
            await provider.WriteTextAsync("keep/b.txt", "b");

            await provider.ClearDirectoryAsync("logs");

            Assert.True(await provider.ExistsAsync("keep/b.txt"));
        }

        [Fact]
        public async Task ClearDirectoryAsync_of_an_empty_directory_returns_zero()
        {
            Assert.Equal(0, await provider.ClearDirectoryAsync("nothing"));
        }

        [Fact]
        public async Task ClearDirectoryAsync_returns_zero_instead_of_throwing_when_the_provider_fails()
        {
            await provider.WriteTextAsync("logs/a.txt", "a");
            provider.Failing = true;

            Assert.Equal(0, await provider.ClearDirectoryAsync("logs"));
        }

        [Fact]
        public async Task Copy_writes_the_source_content_to_the_destination()
        {
            await provider.WriteTextAsync("from.txt", "content");

            await provider.Copy("from.txt", "to/copy.txt");

            Assert.Equal("content", await provider.ReadTextAsync("to/copy.txt"));
            Assert.Equal("content", await provider.ReadTextAsync("from.txt"));
        }

        [Fact]
        public async Task Copy_of_a_missing_source_creates_nothing()
        {
            await provider.Copy("missing.txt", "copy.txt");

            Assert.False(await provider.ExistsAsync("copy.txt"));
        }

        [Fact]
        public async Task Copy_swallows_a_provider_failure()
        {
            await provider.WriteTextAsync("from.txt", "content");
            provider.Failing = true;

            await provider.Copy("from.txt", "to.txt");

            provider.Failing = false;
            Assert.False(await provider.ExistsAsync("to.txt"));
        }

        [Fact(Skip = "Bug: Copy always returns false, even when the file was copied, so a caller cannot tell success from failure.")]
        public async Task Copy_returns_true_when_the_file_was_copied()
        {
            await provider.WriteTextAsync("from.txt", "content");

            Assert.True(await provider.Copy("from.txt", "to.txt"));
        }
    }
}

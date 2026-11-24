using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using cl2j.FileStorage.Provider.Disk;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace cl2j.FileStorage.Disk.Tests
{
    /// <summary>
    /// What <see cref="BufferedFileStorage"/> does with the files it names: clearing, numbering
    /// when a file is full, and surviving a provider that fails. The clock and the timer have
    /// their own tests.
    /// </summary>
    public sealed class BufferedFileStorageBehaviourTests : IDisposable
    {
        private const string Pattern = "log_{0:yyyyMMdd}_{1:00}.log";
        private static readonly DateTime Now = new(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);

        private readonly string root;
        private readonly FileStorageProviderDisk provider;

        public BufferedFileStorageBehaviourTests()
        {
            root = Path.Combine(Path.GetTempPath(), "cl2j-buffered-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Disk:Path"] = root })
                .Build();

            provider = new FileStorageProviderDisk();
            provider.Initialize("Disk", configuration.GetSection("Disk"));
        }

        public void Dispose()
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }

        [Fact]
        public async Task Clearing_the_file_at_startup_deletes_the_one_of_the_day()
        {
            await provider.WriteTextAsync("log_20260905_01.log", "old content");

            using var buffered = new BufferedFileStorage(provider, Pattern, 1024, TimeSpan.FromHours(1), clearFile: true, () => Now);

            Assert.False(await provider.ExistsAsync("log_20260905_01.log"));
        }

        [Fact]
        public async Task Without_clearing_the_existing_file_is_appended_to()
        {
            await provider.WriteTextAsync("log_20260905_01.log", "old|");
            using var buffered = new BufferedFileStorage(provider, Pattern, 1024, TimeSpan.FromHours(1), clearFile: false, () => Now);

            await buffered.AppendAsync("new");
            await buffered.FlushAsync();

            Assert.Equal("old|new", await provider.ReadTextAsync("log_20260905_01.log"));
        }

        [Fact]
        public async Task A_full_file_is_left_behind_and_the_next_number_is_used()
        {
            await provider.WriteTextAsync("log_20260905_01.log", "0123456789");
            using var buffered = new BufferedFileStorage(provider, Pattern, 10, TimeSpan.FromHours(1), clearFile: false, () => Now);

            await buffered.AppendAsync("new");
            await buffered.FlushAsync();

            Assert.Equal("log_20260905_02.log", buffered.CurrentFileName);
            Assert.Equal("0123456789", await provider.ReadTextAsync("log_20260905_01.log"));
            Assert.Equal("new", await provider.ReadTextAsync("log_20260905_02.log"));
        }

        [Fact]
        public async Task A_file_that_grows_past_the_maximum_size_rolls_over_to_the_next_number()
        {
            using var buffered = new BufferedFileStorage(provider, Pattern, 10, TimeSpan.FromHours(1), clearFile: true, () => Now);

            await buffered.AppendAsync("first line is long");
            await buffered.FlushAsync();
            await buffered.AppendAsync("second");
            await buffered.FlushAsync();

            Assert.Equal("first line is long", await provider.ReadTextAsync("log_20260905_01.log"));
            Assert.Equal("second", await provider.ReadTextAsync("log_20260905_02.log"));
        }

        [Fact]
        public async Task A_failing_provider_does_not_make_the_flush_throw_and_the_write_is_attempted_twice()
        {
            var failing = new FailingAppendProvider();
            using var buffered = new BufferedFileStorage(failing, Pattern, 1024, TimeSpan.FromHours(1), clearFile: false, () => Now);

            await buffered.AppendAsync("lost");
            await buffered.FlushAsync();

            Assert.Equal(2, failing.AppendAttempts);
        }

        [Fact]
        public async Task Disposing_flushes_what_is_still_buffered()
        {
            var buffered = new BufferedFileStorage(provider, Pattern, 1024, TimeSpan.FromHours(1), clearFile: true, () => Now);
            await buffered.AppendAsync("pending");

            buffered.Dispose();

            Assert.Equal("pending", await provider.ReadTextAsync("log_20260905_01.log"));
        }

        private sealed class FailingAppendProvider : IFileStorageProvider
        {
            public int AppendAttempts { get; private set; }

            public string Name { get; set; } = "failing";

            public void Initialize(string providerName, IConfigurationSection configuration) { }

            public Task<bool> ExistsAsync(string name) => Task.FromResult(false);

            public Task<FileStoreFileInfo?> GetInfoAsync(string name) => Task.FromResult<FileStoreFileInfo?>(null);

            public Task<IEnumerable<string>> ListFilesAsync(string path) => Task.FromResult(Enumerable.Empty<string>());

            public Task<IEnumerable<string>> ListFoldersAsync(string path) => Task.FromResult(Enumerable.Empty<string>());

            public Task<bool> ReadAsync(string name, Stream stream) => Task.FromResult(false);

            public Task WriteAsync(string name, Stream stream, string? contentType = null) => Task.CompletedTask;

            public Task AppendAsync(string name, Stream stream)
            {
                ++AppendAttempts;
                throw new IOException("Disk full.");
            }

            public Task DeleteAsync(string name) => Task.CompletedTask;
        }
    }
}

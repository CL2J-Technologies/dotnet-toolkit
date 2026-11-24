using System.Text;
using cl2j.FileStorage.Extensions;
using cl2j.FileStorage.Provider.Disk;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace cl2j.FileStorage.Disk.Tests
{
    /// <summary>
    /// Initialization, metadata, listings and error paths of the disk provider. The atomic write
    /// has its own tests in <see cref="FileStorageProviderDiskWriteTests"/>.
    /// </summary>
    public sealed class FileStorageProviderDiskTests : IDisposable
    {
        private readonly string root;
        private readonly FileStorageProviderDisk provider;

        public FileStorageProviderDiskTests()
        {
            root = Path.Combine(Path.GetTempPath(), "cl2j-filestorage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            provider = Create(root);
        }

        public void Dispose()
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }

        private static FileStorageProviderDisk Create(string path, string name = "Disk")
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { [$"{name}:Path"] = path })
                .Build();

            var created = new FileStorageProviderDisk();
            created.Initialize(name, configuration.GetSection(name));
            return created;
        }

        private sealed class NonSeekableStream : MemoryStream
        {
            public override bool CanSeek => false;

            public override long Length => throw new NotSupportedException();
        }

        [Fact]
        public void Initialize_names_the_provider()
        {
            Assert.Equal("Disk", provider.Name);
        }

        [Fact]
        public void Initialize_without_a_path_throws()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Disk:Type"] = "Disk" }).Build();

            Assert.Throws<NotFoundException>(() => new FileStorageProviderDisk().Initialize("Disk", configuration.GetSection("Disk")));
        }

        [Fact]
        public async Task Initialize_creates_the_root_directory_when_it_does_not_exist()
        {
            var missing = Path.Combine(root, "not", "yet");

            var created = Create(missing);
            await created.WriteTextAsync("file.txt", "hello");

            Assert.True(File.Exists(Path.Combine(missing, "file.txt")));
        }

        [Fact]
        public async Task GetInfoAsync_describes_a_file_that_was_written()
        {
            await provider.WriteTextAsync("info.txt", "12345");

            var info = await provider.GetInfoAsync("info.txt");

            Assert.NotNull(info);
            Assert.Equal(5, info.Size);
            Assert.True(Math.Abs((DateTime.UtcNow - info.LastModified).TotalMinutes) < 5);
            Assert.True(Math.Abs((DateTime.UtcNow - info.CreatedOn).TotalMinutes) < 5);
        }

        [Fact]
        public async Task GetInfoAsync_of_a_missing_file_is_null()
        {
            Assert.Null(await provider.GetInfoAsync("missing.txt"));
        }

        [Fact]
        public async Task GetInfoAsync_of_a_name_the_file_system_rejects_is_null()
        {
            Assert.Null(await provider.GetInfoAsync("bad\0name.txt"));
        }

        [Fact]
        public async Task ListFilesAsync_of_a_folder_that_does_not_exist_is_empty()
        {
            Assert.Empty(await provider.ListFilesAsync("nowhere"));
        }

        [Fact]
        public async Task ListFoldersAsync_of_a_folder_that_does_not_exist_is_empty()
        {
            Assert.Empty(await provider.ListFoldersAsync("nowhere"));
        }

        [Fact]
        public async Task ListFilesAsync_does_not_list_the_subfolders()
        {
            await provider.WriteTextAsync(Path.Combine("dir", "a.txt"), "a");
            await provider.WriteTextAsync(Path.Combine("dir", "sub", "b.txt"), "b");

            Assert.Equal(["a.txt"], await provider.ListFilesAsync("dir"));
            Assert.Equal(["sub"], await provider.ListFoldersAsync("dir"));
        }

        [Fact]
        public async Task ReadAsync_of_a_missing_file_returns_false_and_leaves_the_stream_empty()
        {
            using var stream = new MemoryStream();

            Assert.False(await provider.ReadAsync("missing.txt", stream));
            Assert.Equal(0, stream.Length);
        }

        [Fact]
        public async Task DeleteAsync_of_a_missing_file_does_not_throw()
        {
            await provider.DeleteAsync("missing.txt");

            Assert.False(await provider.ExistsAsync("missing.txt"));
        }

        [Fact]
        public async Task A_failed_write_leaves_no_temporary_and_keeps_the_previous_content()
        {
            await provider.WriteTextAsync("kept.txt", "before");

            await Assert.ThrowsAsync<NotSupportedException>(() => provider.WriteAsync("kept.txt", new NonSeekableStream(), null));

            Assert.Equal("before", await provider.ReadTextAsync("kept.txt"));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }

        [Fact]
        public async Task AppendAsync_adds_the_bytes_after_the_existing_content()
        {
            await provider.AppendAsync("append.txt", new MemoryStream(Encoding.UTF8.GetBytes("ab")));
            await provider.AppendAsync("append.txt", new MemoryStream(Encoding.UTF8.GetBytes("cd")));

            Assert.Equal("abcd", await provider.ReadTextAsync("append.txt"));
        }
    }
}

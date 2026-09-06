using cl2j.FileStorage.Provider.Disk;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace cl2j.FileStorage.Disk.Tests
{
    /// <summary>
    /// Tests for the atomic write of the disk provider.
    ///
    /// They exist because of a precise risk: `WriteAsync` opened the target with
    /// `FileMode.Create`, which truncates it to zero before writing. On Appartogo, the machine
    /// that aggregates is deallocated every day by a Logic App while cycles write every three
    /// minutes — an interruption between the truncation and the end of the write would have left
    /// an empty or partial file.
    ///
    /// Atomicity itself cannot be tested deterministically: it would take killing the process at
    /// the exact instant. What can be tested, and is here, are its observable consequences — no
    /// temporary survives, no temporary shows up in a listing, a replacement leaves exactly the
    /// new content, and a read in progress no longer blocks the write.
    /// </summary>
    public sealed class FileStorageProviderDiskWriteTests : IDisposable
    {
        private readonly string root;
        private readonly FileStorageProviderDisk provider;

        public FileStorageProviderDiskWriteTests()
        {
            root = Path.Combine(Path.GetTempPath(), "cl2j-filestorage-" + Guid.NewGuid().ToString("N"));
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
        public async Task WriteAsync_then_ReadAsync_returns_the_same_content()
        {
            var content = Content(5000);

            await WriteFileAsync("file.bin", content);

            Assert.Equal(content, await ReadFileAsync("file.bin"));
        }

        [Fact]
        public async Task WriteAsync_leaves_no_temporary_behind()
        {
            await WriteFileAsync("file.bin", Content(5000));

            var temporaires = Directory.GetFiles(root, "*.tmp");
            Assert.Empty(temporaires);
        }

        [Fact]
        public async Task WriteAsync_fully_replaces_a_longer_file()
        {
            //The classic trap of a write that would not overwrite the whole target: the tail of
            //the old content would stay stuck to the new one.
            await WriteFileAsync("file.bin", Content(20000));
            var shorter = Content(300);

            await WriteFileAsync("file.bin", shorter);

            Assert.Equal(shorter, await ReadFileAsync("file.bin"));
        }

        [Fact]
        public async Task WriteAsync_succeeds_while_a_read_holds_the_file()
        {
            //Regression guard: replacing by rename would fail on Windows if the file being
            //replaced were open without FileShare.Delete. Truncation, for its part, required
            //nothing — this test checks we did not break that case while gaining atomicity.
            await WriteFileAsync("file.bin", Content(1000));

            var path = Path.Combine(root, "file.bin");
            using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var updated = Content(2000);
                await WriteFileAsync("file.bin", updated);
                Assert.Equal(updated, await ReadFileAsync("file.bin"));
            }
        }

        [Fact]
        public async Task ListFilesAsync_leaves_out_temporaries()
        {
            //A process killed mid-write can leave a temporary behind. It must never pass for data
            //in the eyes of a caller scanning the folder.
            await WriteFileAsync("file.bin", Content(100));
            await File.WriteAllTextAsync(Path.Combine(root, "file.bin.abcdef.tmp"), "reliquat");

            var files = (await provider.ListFilesAsync(string.Empty)).ToList();

            Assert.Contains("file.bin", files);
            Assert.DoesNotContain(files, f => f.EndsWith(".tmp", StringComparison.Ordinal));
        }

        [Fact]
        public async Task WriteAsync_creates_missing_folders()
        {
            var content = Content(200);

            await WriteFileAsync("un/deux/trois.bin", content);

            Assert.Equal(content, await ReadFileAsync("un/deux/trois.bin"));
        }

        [Fact]
        public async Task WriteAsync_succeeds_when_a_third_party_holds_the_target_then_releases_it()
        {
            // Reproduces the failure of September 4th 2026, seen on the crawler VM:
            // "Unable to remove the file to be replaced" — Windows error 1175. File.Replace has to
            // delete the old target, which fails as long as a third party holds it open without
            // FileShare.Delete. An antivirus scanning the file just written is enough, hence the
            // intermittent nature.
            //
            // The lock is released after 120 ms — inside the window of five attempts, which cover
            // half a second. Without the retry, this call throws.
            await WriteFileAsync("file.bin", Content(1000));
            var path = Path.Combine(root, "file.bin");

            var fileLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var relache = Task.Run(async () =>
            {
                await Task.Delay(120);
                fileLock.Dispose();
            });

            var updated = Content(2000);
            await WriteFileAsync("file.bin", updated);
            await relache;

            Assert.Equal(updated, await ReadFileAsync("file.bin"));
            Assert.Empty(Directory.GetFiles(root, "*.tmp", SearchOption.AllDirectories));
        }

        private async Task WriteFileAsync(string nom, byte[] content)

        {
            using var source = new MemoryStream(content);
            await provider.WriteAsync(nom, source, null);
        }

        private async Task<byte[]> ReadFileAsync(string nom)
        {
            using var destination = new MemoryStream();
            Assert.True(await provider.ReadAsync(nom, destination));
            return destination.ToArray();
        }

        private static byte[] Content(int size)
        {
            var bytes = new byte[size];
            for (var i = 0; i < size; ++i)
                bytes[i] = (byte)(i % 251);
            return bytes;
        }
    }
}

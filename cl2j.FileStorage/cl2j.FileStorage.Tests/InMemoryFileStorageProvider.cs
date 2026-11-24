using cl2j.FileStorage.Core;
using Microsoft.Extensions.Configuration;

namespace cl2j.FileStorage.Tests
{
    /// <summary>
    ///     A provider that keeps its files in a dictionary, so the extension methods can be tested
    ///     without touching a disk. It records the content type of each write, and can be told to
    ///     fail the operations that matter to the tests.
    /// </summary>
    internal sealed class InMemoryFileStorageProvider : IFileStorageProvider
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public Dictionary<string, string?> ContentTypes { get; } = [];

        public bool Failing { get; set; }

        public string Name { get; set; } = "memory";

        public void Initialize(string providerName, IConfigurationSection configuration) => Name = providerName;

        public Task<bool> ExistsAsync(string name) => Task.FromResult(Files.ContainsKey(Normalize(name)));

        public Task<FileStoreFileInfo?> GetInfoAsync(string name) =>
            Task.FromResult(Files.TryGetValue(Normalize(name), out var bytes) ? new FileStoreFileInfo { Size = bytes.Length } : null);

        public Task<IEnumerable<string>> ListFilesAsync(string path)
        {
            ThrowIfFailing();
            var prefix = Normalize(path).TrimEnd('/') + "/";
            var names = Files.Keys
                .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
                .Select(key => key[prefix.Length..])
                .Where(name => !name.Contains('/'))
                .ToList();
            return Task.FromResult<IEnumerable<string>>(names);
        }

        public Task<IEnumerable<string>> ListFoldersAsync(string path) => Task.FromResult(Enumerable.Empty<string>());

        public async Task<bool> ReadAsync(string name, Stream stream)
        {
            ThrowIfFailing();
            if (!Files.TryGetValue(Normalize(name), out var bytes))
                return false;
            await stream.WriteAsync(bytes);
            return true;
        }

        public async Task WriteAsync(string name, Stream stream, string? contentType = null)
        {
            ThrowIfFailing();
            using var copy = new MemoryStream();
            stream.Seek(0, SeekOrigin.Begin);
            await stream.CopyToAsync(copy);
            Files[Normalize(name)] = copy.ToArray();
            ContentTypes[Normalize(name)] = contentType;
        }

        public Task AppendAsync(string name, Stream stream) => throw new NotSupportedException();

        public Task DeleteAsync(string name)
        {
            ThrowIfFailing();
            Files.Remove(Normalize(name));
            return Task.CompletedTask;
        }

        // Callers build names with Path.Combine, which uses the separator of the platform.
        private static string Normalize(string name) => name.Replace('\\', '/');

        private void ThrowIfFailing()
        {
            if (Failing)
                throw new IOException("The provider is failing.");
        }
    }
}

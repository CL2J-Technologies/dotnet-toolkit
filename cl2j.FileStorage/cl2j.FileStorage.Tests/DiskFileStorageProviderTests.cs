using cl2j.FileStorage.Core;
using cl2j.FileStorage.Provider.Disk;
using Microsoft.Extensions.Configuration;

namespace cl2j.FileStorage.Tests
{
    /// <summary>
    ///     The contract, run against the disk provider.
    ///
    ///     <para>
    ///     The root is a fresh temporary directory. What this replaces read its path from an
    ///     <c>appsettings.json</c> that named <c>C:\Dev\tmpData1</c> — absolute, Windows-only, and
    ///     shared between runs. It could not have passed on the Linux runner even after it was made
    ///     to compile.
    ///     </para>
    /// </summary>
    public sealed class DiskFileStorageProviderTests : FileStorageProviderContract, IDisposable
    {
        private readonly string root;
        private readonly FileStorageProviderDisk provider;

        public DiskFileStorageProviderTests()
        {
            root = Path.Combine(Path.GetTempPath(), "cl2j-filestorage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Disk:Path"] = root })
                .Build();

            provider = new FileStorageProviderDisk();
            provider.Initialize("Disk", configuration.GetSection("Disk"));
        }

        protected override IFileStorageProvider Provider => provider;

        public void Dispose()
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}

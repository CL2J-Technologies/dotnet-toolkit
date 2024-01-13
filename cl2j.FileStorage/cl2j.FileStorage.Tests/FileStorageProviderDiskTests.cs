using System.Threading.Tasks;
using cl2j.FileStorage.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cl2j.FileStorage.Tests
{
    public class FileStorageProviderDiskTests : IAsyncLifetime
    {
        private FileStorageTests fileStorageTests;

        #region Warn up / Cool down

        public Task InitializeAsync()
        {
            var serviceCollection = ConfigurationHelpers.Configure(ConfigurationHelpers.ConfigureServices());
            var serviceProvider = serviceCollection.BuildServiceProvider();
            fileStorageTests = new FileStorageTests("Disk", serviceProvider.GetRequiredService<IFileStorageFactory>().GetProvider("Disk"));
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        #endregion Warn up / Cool down

        #region Tests

        [Fact]
        public async Task WriteAsync_IfExists_DoNotThrow()
        {
            await fileStorageTests.WriteAsync_IfExists_DoNotThrow();
        }

        [Fact]
        public async Task AppendAsync_IfExists_DoNotThrow()
        {
            await fileStorageTests.AppendAsync_IfExists_DoNotThrow();
        }

        [Fact]
        public async Task ListAsync_IfExists_DoNotThrow()
        {
            await fileStorageTests.ListAsync_IfExists_DoNotThrow();
        }

        [Fact]
        public async Task DeleteAsync_IfNotExists_DoNotThrow()
        {
            await fileStorageTests.DeleteAsync_IfNotExists_DoNotThrow();
        }

        #endregion Tests
    }
}
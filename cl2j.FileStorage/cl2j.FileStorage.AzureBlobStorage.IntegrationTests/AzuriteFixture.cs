using Testcontainers.Azurite;
using Xunit;

namespace cl2j.FileStorage.AzureBlobStorage.IntegrationTests
{
    /// <summary>
    ///     One Azurite (the Azure Storage emulator) for the whole assembly, in a container.
    ///
    ///     <para>
    ///     The provider builds its <c>BlobContainerClient</c> itself from a connection string, so
    ///     it cannot be exercised with a mock without a production change; and what it relies on
    ///     (a missing blob answering <c>BlobNotFound</c>, append blobs, prefix listing) is a
    ///     property of the service, visible only by asking one.
    ///     </para>
    ///
    ///     <para>
    ///     The image is pinned rather than floating, for the reason the other fixtures give: a
    ///     suite whose server changes underneath it cannot reproduce its failures.
    ///     <c>--skipApiVersionCheck</c> is there because Azurite refuses requests from an SDK
    ///     newer than the API version it knows, which would turn every SDK upgrade into a red
    ///     build that says nothing about the provider.
    ///     </para>
    ///
    ///     <para>
    ///     These fail rather than skip when Docker is unavailable, like the other integration
    ///     tests. A skipped test reads as a passing one in a summary line, and the skip becomes
    ///     permanent.
    ///     </para>
    /// </summary>
    public sealed class AzuriteFixture : IAsyncLifetime
    {
        private readonly AzuriteContainer container = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0")
            .WithCommand("--skipApiVersionCheck")
            .Build();

        public string ConnectionString => container.GetConnectionString();

        public async Task InitializeAsync() => await container.StartAsync();

        public async Task DisposeAsync() => await container.DisposeAsync();
    }

    [CollectionDefinition(Name)]
    public sealed class AzuriteCollectionDefinition : ICollectionFixture<AzuriteFixture>
    {
        public const string Name = "azurite";
    }
}

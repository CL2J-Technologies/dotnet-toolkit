using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using cl2j.FileStorage.Provider.AzureBlobStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cl2j.FileStorage.AzureBlobStorage.IntegrationTests
{
    /// <summary>
    ///     How an application gets the Azure Blob provider: through the registration extension and
    ///     a configuration section.
    /// </summary>
    [Collection(AzuriteCollectionDefinition.Name)]
    public sealed class AzureRegistrationTests(AzuriteFixture azurite)
    {
        [Fact]
        public async Task The_registration_extension_makes_a_configured_Azure_storage_available_by_name()
        {
            //The factory keeps its providers for the life of the process, so the name is unique.
            var name = "azure-" + Guid.NewGuid().ToString("N")[..12];
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"cl2j:FileStorage:Storages:{name}:Type"] = "AzureBlobStorage",
                [$"cl2j:FileStorage:Storages:{name}:ConnectionString"] = azurite.ConnectionString,
                [$"cl2j:FileStorage:Storages:{name}:Container"] = AzureProviderFactory.NewContainerName("registered"),
                [$"cl2j:FileStorage:Storages:{name}:CreateIfMissing"] = "true"
            }).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfigurationRoot>(configuration);
            services.AddSingleton<IConfiguration>(configuration);

            services.AddAzureBlobFileStorage();
            var provider = services.BuildServiceProvider().GetRequiredService<IFileStorageFactory>().GetProvider(name);

            Assert.IsType<FileStorageProviderAzureBlobStorage>(provider);
            await provider.WriteTextAsync("registered.txt", "through the extension");
            Assert.Equal("through the extension", await provider.ReadTextAsync("registered.txt"));
        }
    }
}

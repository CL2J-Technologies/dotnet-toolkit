using cl2j.FileStorage.Core;
using cl2j.FileStorage.Extensions;
using cl2j.FileStorage.Provider.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cl2j.FileStorage.S3.IntegrationTests
{
    /// <summary>
    ///     How an application gets the S3 provider: through the registration extension and a
    ///     configuration section, and by naming a region instead of a service URL.
    /// </summary>
    [Collection(S3MockCollectionDefinition.Name)]
    public sealed class S3RegistrationTests(S3MockFixture s3mock)
    {
        [Fact]
        public async Task The_registration_extension_makes_a_configured_S3_storage_available_by_name()
        {
            //The factory keeps its providers for the life of the process, so the name is unique.
            var name = "s3-" + Guid.NewGuid().ToString("N")[..12];
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"cl2j:FileStorage:Storages:{name}:Type"] = "S3",
                [$"cl2j:FileStorage:Storages:{name}:Bucket"] = "registered-" + Guid.NewGuid().ToString("N")[..12],
                [$"cl2j:FileStorage:Storages:{name}:AccessKey"] = S3MockFixture.AccessKey,
                [$"cl2j:FileStorage:Storages:{name}:SecretKey"] = S3MockFixture.SecretKey,
                [$"cl2j:FileStorage:Storages:{name}:ServiceUrl"] = s3mock.ServiceUrl,
                [$"cl2j:FileStorage:Storages:{name}:CreateIfMissing"] = "true"
            }).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfigurationRoot>(configuration);
            services.AddSingleton<IConfiguration>(configuration);

            services.AddS3FileStorage();
            var provider = services.BuildServiceProvider().GetRequiredService<IFileStorageFactory>().GetProvider(name);

            Assert.IsType<FileStorageProviderS3>(provider);
            await provider.WriteTextAsync("registered.txt", "through the extension");
            Assert.Equal("through the extension", await provider.ReadTextAsync("registered.txt"));
        }

        [Fact]
        public void A_region_without_a_service_URL_points_the_client_at_that_regions_endpoint()
        {
            //No server is listening on a region that does not exist, so initializing fails
            //reaching the bucket; that it fails on the regional endpoint is what shows the region
            //was used rather than ignored.
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["S3:Bucket"] = "regional",
                ["S3:AccessKey"] = S3MockFixture.AccessKey,
                ["S3:SecretKey"] = S3MockFixture.SecretKey,
                ["S3:Region"] = "no-such-region-1"
            }).Build();

            var ex = Assert.ThrowsAny<Exception>(() => new FileStorageProviderS3().Initialize("S3", configuration.GetSection("S3")));

            Assert.Contains("no-such-region-1", ex.ToString(), StringComparison.Ordinal);
        }
    }
}

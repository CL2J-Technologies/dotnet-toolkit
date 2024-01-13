using Microsoft.Extensions.DependencyInjection;

namespace cl2j.FileStorage.Provider.AzureBlobStorage
{
    public static class FileStorageAzureExtensions
    {
        public static void AddAzureBlobFileStorage(this IServiceCollection services)
        {
            services.AddFileStorage(factory => factory.Register<FileStorageProviderAzureBlobStorage>("AzureBlobStorage"));
        }
    }
}
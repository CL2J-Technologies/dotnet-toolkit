using cl2j.FileStorage.Core;
using cl2j.FileStorage.Provider.Disk;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace cl2j.FileStorage
{
    public static class FileStorageExtensions
    {
        public static void AddFileStorage(this IServiceCollection services, Action<FileStorageFactory>? factoryCallback = null)
        {
            services.TryAddSingleton<IFileStorageFactory>(x =>
            {
                var configuration = x.GetRequiredService<IConfigurationRoot>();
                var fileStorageFactory = new FileStorageFactory(configuration);

                fileStorageFactory.Register<FileStorageProviderDisk>("Disk");

                factoryCallback?.Invoke(fileStorageFactory);

                return fileStorageFactory;
            });
        }

        public static IFileStorageProvider GetFileStorageProvider(this IServiceProvider builder, string datastoreName)
        {
            var fileStorageFactory = builder.GetRequiredService<IFileStorageFactory>();
            var fileStorageProvider = fileStorageFactory.GetProvider(datastoreName) ?? throw new NotFoundException($"DataStore '{datastoreName}' not found");
            return fileStorageProvider;
        }
    }
}
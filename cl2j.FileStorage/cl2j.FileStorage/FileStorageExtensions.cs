using cl2j.FileStorage.Core;
using cl2j.FileStorage.Provider.Disk;
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
    }
}
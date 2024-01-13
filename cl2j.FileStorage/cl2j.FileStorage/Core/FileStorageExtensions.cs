using Microsoft.Extensions.DependencyInjection;

namespace cl2j.FileStorage.Core
{
    public static class FileStorageExtensions
    {
        public static IFileStorageProvider GetProvider(this IServiceProvider serviceProvider, string name)
        {
            var fileStorageFactory = serviceProvider.GetRequiredService<IFileStorageFactory>();
            return fileStorageFactory.GetProvider(name);
        }
    }
}

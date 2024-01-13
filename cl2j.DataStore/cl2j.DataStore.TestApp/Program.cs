using cl2j.DataStore.Json;
using cl2j.FileStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace cl2j.DataStore.TestApp
{
    internal class Program
    {
        private static async Task Main()
        {
            ServiceProvider serviceProvider = ConfigureServices();
            await serviceProvider.GetRequiredService<DataStoreSample>().ExecuteAsync();
        }

        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            services.AddSingleton(configuration);

            services.AddLogging(builder => { builder.AddDebug().SetMinimumLevel(LogLevel.Trace); });

            //Bootstrap the FileStorage to be available from DependencyInjection.
            //This will allow accessing IFileStorageProviderFactory instance
            services.AddFileStorage();

            //Configure the JSON DataStore.
            //The store will use the FileStorageProvider to access the file color.json.
            //The field Id represent the key for the Color class
            services.AddDataStoreJson<string, Color>("DataStore", "colors.json", (color) => color.Id);
            //or add the store with a cache
            //services.AddDataStoreJsonWithCache<string, Color>("DataStore", "colors.json", (color) => color.Id, TimeSpan.FromSeconds(5));

            services.AddSingleton<DataStoreSample>();

            var serviceProvider = services.BuildServiceProvider();

            return serviceProvider;
        }
    }
}
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace cl2j.FileStorage.TestApp
{
    internal class Program
    {
        private static async Task Main()
        {
            ServiceProvider serviceProvider = ConfigureServices();
            await serviceProvider.GetRequiredService<FileOperationSample>().ExecuteAsync();
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

            //Configure the FileStorage DI
            services.AddFileStorage();

            services.AddSingleton<FileOperationSample>();

            var serviceProvider = services.BuildServiceProvider();

            return serviceProvider;
        }
    }
}
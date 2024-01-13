using cl2j.FileStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Threading.Tasks;

namespace cl2j.Logging.TestApp
{
    internal class Program
    {
        private static async Task Main()
        {
            ServiceProvider serviceProvider = ConfigureServices();
            await serviceProvider.GetRequiredService<LoggingSample>().ExecuteAsync();

            serviceProvider.GetRequiredService<ILoggerProvider>()?.Dispose();
        }

        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            services.AddSingleton(configuration);

            //Configure the FileStorage DI
            services.AddFileStorage();

            services.AddLogging(configuration);

            services.AddSingleton<LoggingSample>();

            var serviceProvider = services.BuildServiceProvider();

            return serviceProvider;
        }
    }
}
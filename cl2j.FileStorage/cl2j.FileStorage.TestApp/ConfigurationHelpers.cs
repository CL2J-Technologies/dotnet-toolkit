using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO;

namespace cl2j.FileStorage.TestApp
{
    internal class ConfigurationHelpers
    {
        public static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            services.AddSingleton(configuration);

            services.AddLogging(builder => { builder.AddDebug().SetMinimumLevel(LogLevel.Trace); });

            services.AddFileStorage();

            return services.BuildServiceProvider();
        }
    }
}
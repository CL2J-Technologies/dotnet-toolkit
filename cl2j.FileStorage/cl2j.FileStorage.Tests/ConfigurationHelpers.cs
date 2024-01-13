using System.IO;
using cl2j.FileStorage.Provider.AzureBlobStorage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace cl2j.FileStorage.Tests
{
    internal class ConfigurationHelpers
    {
        public static IServiceCollection ConfigureServices()
        {
            var services = new ServiceCollection();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            services.AddSingleton(configuration);

            services.AddLogging(builder => { builder.AddDebug().SetMinimumLevel(LogLevel.Trace); });

            services.AddFileStorage();

            return services;
        }

        public static IServiceCollection Configure(IServiceCollection serviceProvider)
        {
            //Add the Azure provider
            serviceProvider.AddAzureBlobFileStorage();

            return serviceProvider;
        }
    }
}
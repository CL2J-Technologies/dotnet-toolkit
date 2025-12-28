using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace cl2j.Logging
{
    public static class LoggingExtensions
    {
        private const string LoggingSectionName = "cl2j:Logging";

        public static IServiceCollection AddLogging(this IServiceCollection services, IConfiguration configuration, string? applicationName = null)
        {
            // Setting Setting Minimum Log Level from AppSettings
            var loggingSection = configuration.GetSection(LoggingSectionName);
            services.AddLogging(loggingBuilder =>
            {
                var defaultLogLevel = loggingSection?.GetValue<LogLevel?>("LogLevel");
                if (defaultLogLevel != null)
                {
                    loggingBuilder.SetMinimumLevel(defaultLogLevel.Value);
                    AddFilter(loggingBuilder, "", defaultLogLevel.Value);
                    return;
                }
            });

            // Registering LoggerProvider to handle FileStorage + Console + Debug Logging
            services.AddSingleton<ILoggerProvider, LoggerProvider>();
            services.Configure<LoggerOptions>(options =>
            {
                //MainFileNamePattern will be overwritten by the Bind if provided in the config
                var instanceName = configuration.GetValue<string>("WEBSITE_INSTANCE_ID");
                var instanceInfo = string.IsNullOrEmpty(instanceName) ? "" : "_" + instanceName;
                if (instanceInfo.Length > 8)
                    instanceInfo = instanceInfo[..8];
                if (string.IsNullOrEmpty(applicationName))
                    applicationName = configuration.GetValue<string>("ApplicationName");
                options.MainFileNamePattern = applicationName + instanceInfo + "_{0:yyyyMMdd}_{1:00}.log";

                configuration.Bind(LoggingSectionName, options);
            });

            return services;
        }

        public static IServiceCollection AddLogging(this IServiceCollection services, IConfigurationRoot configuration, string? applicationName = null)
        {
            return AddLogging(services, (IConfiguration)configuration, applicationName);
        }

        private static void AddFilter(ILoggingBuilder loggingBuilder, string key, LogLevel logLevel)
        {
            loggingBuilder.AddFilter(key, logLevel);
        }
    }
}
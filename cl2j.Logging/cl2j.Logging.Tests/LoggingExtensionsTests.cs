using cl2j.FileStorage.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace cl2j.Logging.Tests
{
    public sealed class LoggingExtensionsTests : IDisposable
    {
        private readonly LoggingTestHarness harness = new();

        public void Dispose() => harness.Dispose();

        private static IConfigurationRoot Configuration(Dictionary<string, string?> values) =>
            new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        private ServiceProvider Build(IConfigurationRoot configuration, string? applicationName = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton(harness.Factory);
            LoggingExtensions.AddLogging(services, configuration, applicationName);
            return services.BuildServiceProvider();
        }

        private static LoggerOptions OptionsOf(ServiceProvider provider) => provider.GetRequiredService<IOptions<LoggerOptions>>().Value;

        [Fact]
        public void AddLogging_registers_the_cl2j_provider()
        {
            using var provider = Build(Configuration(new() { ["ApplicationName"] = "shop" }));

            Assert.Contains(provider.GetServices<ILoggerProvider>(), p => p is LoggerProvider);
        }

        [Fact]
        public void The_file_name_pattern_defaults_to_the_application_name_from_the_configuration()
        {
            using var provider = Build(Configuration(new() { ["ApplicationName"] = "shop" }));

            Assert.Equal("shop_{0:yyyyMMdd}_{1:00}.log", OptionsOf(provider).MainFileNamePattern);
        }

        [Fact]
        public void The_application_name_argument_wins_over_the_configuration()
        {
            using var provider = Build(Configuration(new() { ["ApplicationName"] = "shop" }), "billing");

            Assert.Equal("billing_{0:yyyyMMdd}_{1:00}.log", OptionsOf(provider).MainFileNamePattern);
        }

        [Fact]
        public void The_instance_id_is_added_to_the_file_name_and_truncated_to_eight_characters()
        {
            using var provider = Build(Configuration(new() { ["ApplicationName"] = "shop", ["WEBSITE_INSTANCE_ID"] = "abcdef0123456789" }));

            // "_" + the first seven characters of the id: eight in all.
            Assert.Equal("shop_abcdef0_{0:yyyyMMdd}_{1:00}.log", OptionsOf(provider).MainFileNamePattern);
        }

        [Fact]
        public void A_short_instance_id_is_kept_whole()
        {
            using var provider = Build(Configuration(new() { ["ApplicationName"] = "shop", ["WEBSITE_INSTANCE_ID"] = "web1" }));

            Assert.Equal("shop_web1_{0:yyyyMMdd}_{1:00}.log", OptionsOf(provider).MainFileNamePattern);
        }

        [Fact]
        public void The_cl2j_Logging_section_binds_onto_the_options_and_overrides_the_pattern()
        {
            using var provider = Build(Configuration(new()
            {
                ["ApplicationName"] = "shop",
                ["cl2j:Logging:MainFileNamePattern"] = "custom_{0:yyyyMMdd}.log",
                ["cl2j:Logging:TimeZoneName"] = "UTC",
                ["cl2j:Logging:MaxSize"] = "1234",
                ["cl2j:Logging:FlushInterval"] = "00:00:30",
                ["cl2j:Logging:IncludeConsole"] = "true",
                ["cl2j:Logging:ClearOnStartup"] = "true",
                ["cl2j:Logging:MemoryLogs"] = "true",
            }));

            var options = OptionsOf(provider);
            Assert.Equal("custom_{0:yyyyMMdd}.log", options.MainFileNamePattern);
            Assert.Equal("UTC", options.TimeZoneName);
            Assert.Equal(1234, options.MaxSize);
            Assert.Equal(TimeSpan.FromSeconds(30), options.FlushInterval);
            Assert.True(options.IncludeConsole);
            Assert.True(options.ClearOnStartup);
            Assert.True(options.MemoryLogs);
        }

        [Fact]
        public void The_options_keep_their_defaults_when_the_section_is_absent()
        {
            using var provider = Build(Configuration(new() { ["ApplicationName"] = "shop" }));

            var options = OptionsOf(provider);
            Assert.Equal(3 * 1024 * 1024, options.MaxSize);
            Assert.Equal(TimeSpan.FromSeconds(5), options.FlushInterval);
            Assert.False(options.IncludeConsole);
            Assert.False(options.ClearOnStartup);
            Assert.False(options.MemoryLogs);
        }

        [Fact]
        public void The_configured_LogLevel_filters_what_the_provider_receives()
        {
            var configuration = Configuration(new()
            {
                ["ApplicationName"] = "shop",
                ["cl2j:Logging:LogLevel"] = "Warning",
                ["cl2j:Logging:TimeZoneName"] = "UTC",
                ["cl2j:Logging:FlushInterval"] = "01:00:00",
                ["cl2j:Logging:MemoryLogs"] = "true",
            });
            using var provider = Build(configuration);
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("filtered");

            logger.LogInformation("dropped");
            logger.LogWarning("kept");

            var memory = provider.GetServices<ILoggerProvider>().OfType<LoggerProvider>().Single().MemoryLogger;
            Assert.Equal(["kept"], memory.GetLogs().Select(l => l.Text));
        }

        [Fact]
        public void Without_a_configured_LogLevel_the_provider_adds_no_filter_of_its_own()
        {
            var configuration = Configuration(new()
            {
                ["ApplicationName"] = "shop",
                ["cl2j:Logging:TimeZoneName"] = "UTC",
                ["cl2j:Logging:FlushInterval"] = "01:00:00",
                ["cl2j:Logging:MemoryLogs"] = "true",
            });
            using var provider = Build(configuration);
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("open");

            logger.LogWarning("w");
            logger.LogInformation("i");

            var memory = provider.GetServices<ILoggerProvider>().OfType<LoggerProvider>().Single().MemoryLogger;
            Assert.Equal(["w", "i"], memory.GetLogs().Select(l => l.Text));
        }

        [Fact]
        public async Task A_logger_from_the_container_writes_to_the_file_storage_named_Logger()
        {
            var configuration = Configuration(new()
            {
                ["ApplicationName"] = "shop",
                ["cl2j:Logging:TimeZoneName"] = "UTC",
                ["cl2j:Logging:FlushInterval"] = "01:00:00",
            });
            var provider = Build(configuration);

            provider.GetRequiredService<ILoggerFactory>().CreateLogger("end-to-end").LogError("written through DI");
            provider.Dispose();

            var file = Assert.Single(await harness.LogFilesAsync());
            Assert.StartsWith("shop_", Path.GetFileName(file));
            Assert.Contains("ERR [end-to-end] written through DI", File.ReadAllText(file));
        }

        [Fact]
        public void The_IConfiguration_overload_behaves_like_the_IConfigurationRoot_one()
        {
            IConfiguration configuration = Configuration(new() { ["ApplicationName"] = "shop" });
            var services = new ServiceCollection();
            services.AddSingleton<IFileStorageFactory>(harness.Factory);

            LoggingExtensions.AddLogging(services, configuration);
            using var provider = services.BuildServiceProvider();

            Assert.Equal("shop_{0:yyyyMMdd}_{1:00}.log", OptionsOf(provider).MainFileNamePattern);
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     <c>UseDatabase</c> on a host: what the application's own container knows is handed to
    ///     the static holder the execution path reads.
    /// </summary>
    public class UseDatabaseHostTests
    {
        private sealed class StubHost(IServiceProvider services) : IHost
        {
            public IServiceProvider Services => services;

            public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

            public void Dispose()
            {
            }
        }

        [Fact]
        public void The_options_and_the_logger_of_a_host_are_published_to_the_static_holder()
        {
            var previousOptions = ConnectionExtensions.DatabaseOptions;
            var previousLogger = ConnectionExtensions.Logger;
            try
            {
                var logger = NullLogger.Instance;
                var services = new ServiceCollection();
                services.AddSingleton<ILogger>(logger);
                services.AddDatabase(o => o.BulkInsertTimeout = TimeSpan.FromSeconds(77));
                using var provider = services.BuildServiceProvider();

                new StubHost(provider).UseDatabase();

                Assert.Equal(TimeSpan.FromSeconds(77), ConnectionExtensions.DatabaseOptions.BulkInsertTimeout);
                Assert.Same(logger, ConnectionExtensions.Logger);
            }
            finally
            {
                ConnectionExtensions.DatabaseOptions = previousOptions;
                ConnectionExtensions.Logger = previousLogger;
            }
        }

        [Fact]
        public void A_default_host_publishes_a_logger_from_its_logger_factory()
        {
            var previousOptions = ConnectionExtensions.DatabaseOptions;
            var previousLogger = ConnectionExtensions.Logger;
            try
            {
                ConnectionExtensions.Logger = null;
                //A default host registers ILoggerFactory and ILogger<T>, never a bare ILogger.
                using var host = Host.CreateDefaultBuilder().Build();

                host.UseDatabase();

                Assert.NotNull(ConnectionExtensions.Logger);
            }
            finally
            {
                ConnectionExtensions.DatabaseOptions = previousOptions;
                ConnectionExtensions.Logger = previousLogger;
            }
        }

        [Fact]
        public void A_service_provider_with_standard_logging_publishes_a_logger_from_its_logger_factory()
        {
            var previousOptions = ConnectionExtensions.DatabaseOptions;
            var previousLogger = ConnectionExtensions.Logger;
            try
            {
                ConnectionExtensions.Logger = null;
                using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();

                provider.UseDatabase();

                Assert.NotNull(ConnectionExtensions.Logger);
            }
            finally
            {
                ConnectionExtensions.DatabaseOptions = previousOptions;
                ConnectionExtensions.Logger = previousLogger;
            }
        }

        [Fact]
        public void A_host_that_registered_nothing_leaves_the_current_options_alone()
        {
            var previousOptions = ConnectionExtensions.DatabaseOptions;
            var previousLogger = ConnectionExtensions.Logger;
            try
            {
                using var provider = new ServiceCollection().BuildServiceProvider();

                new StubHost(provider).UseDatabase();

                Assert.Same(previousOptions, ConnectionExtensions.DatabaseOptions);
                Assert.Null(ConnectionExtensions.Logger);
            }
            finally
            {
                ConnectionExtensions.DatabaseOptions = previousOptions;
                ConnectionExtensions.Logger = previousLogger;
            }
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     SQL Server's run of the shared <see cref="KnownDefectTestsBase"/> (the defects that
    ///     reach a server), plus the one that does not and is listed once, here.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class KnownDefectTests(SqlServerFixture fixture) : KnownDefectTestsBase(fixture)
    {
        [Fact(Skip = "Bug: #68 UseDatabase resolves a non-generic ILogger, which a default host does not register, so the static logger stays null")]
        public void UseDatabase_on_a_standard_logging_setup_publishes_a_logger()
        {
            var previous = ConnectionExtensions.Logger;
            try
            {
                ConnectionExtensions.Logger = null;
                //What a default host registers for logging: ILogger<T> and ILoggerFactory, no bare ILogger.
                using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();

                provider.UseDatabase();

                Assert.NotNull(ConnectionExtensions.Logger);
            }
            finally
            {
                ConnectionExtensions.Logger = previous;
            }
        }
    }
}

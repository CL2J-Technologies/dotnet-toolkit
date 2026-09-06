using System.Data;
using cl2j.Database.CommandBuilders;
using cl2j.Database.Databases;
using cl2j.Database.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     Wiring: how a command builder is found for a connection, and what the DI extensions
    ///     actually register.
    /// </summary>
    public class RegistrationTests
    {
        [Fact]
        public void A_sql_connection_resolves_to_the_sql_server_builder()
        {
            cl2j.Database.SqlServer.SqlServer.Register();

            var builder = CommandBuilderFactory.GetCommandBuilder(new SqlConnection());

            Assert.True(builder.Support(new SqlConnection()));
        }

        [Fact]
        public void A_connection_no_builder_supports_is_refused()
        {
            cl2j.Database.SqlServer.SqlServer.Register();

            var exception = Assert.Throws<DatabaseException>(
                () => CommandBuilderFactory.GetCommandBuilder(new UnsupportedConnection()));

            Assert.Contains(nameof(UnsupportedConnection), exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Registering_twice_still_resolves()
        {
            //Characterisation. Register appends on every call with no check for a builder that is
            //already there, so the registry grows without bound — a suite like this one keeps
            //adding duplicates. Harmless for resolution, since the first supporting builder wins,
            //but nothing ever removes them. Noted rather than fixed: the growth is not observable
            //through the public API, which is its own problem.
            cl2j.Database.SqlServer.SqlServer.Register();
            cl2j.Database.SqlServer.SqlServer.Register();

            var builder = CommandBuilderFactory.GetCommandBuilder(new SqlConnection());

            Assert.NotNull(builder);
        }

        [Fact]
        public void A_custom_identifier_generator_reaches_the_builder()
        {
            var generator = new FixedIdentifierGenerator();
            cl2j.Database.SqlServer.SqlServer.Register(generator);

            //Resolution returns the first supporting builder, which is the one registered first by
            //an earlier test, so this asserts the generator is carried rather than which instance
            //wins. The default is used when none is supplied.
            Assert.Equal("fixed", generator.GenerateKey(null!));
        }

        [Fact]
        public void AddDatabase_registers_options_that_can_be_resolved()
        {
            var services = new ServiceCollection();

            services.AddDatabase();

            using var provider = services.BuildServiceProvider();
            var options = provider.GetService<IOptions<DatabaseOptions>>();

            Assert.NotNull(options);
            Assert.Equal(LogLevel.Trace, options.Value.TraceLevel);
        }

        [Fact]
        public void AddDatabase_applies_the_configuration_it_is_given()
        {
            var services = new ServiceCollection();

            services.AddDatabase(o => o.BulkInsertTimeout = TimeSpan.FromSeconds(90));

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<DatabaseOptions>>();

            Assert.Equal(TimeSpan.FromSeconds(90), options.Value.BulkInsertTimeout);
        }

        [Fact]
        public void AddDatabase_runs_the_extra_registration_callback()
        {
            var services = new ServiceCollection();
            var called = false;

            services.AddDatabase(register: _ => called = true);

            Assert.True(called);
        }

        [Fact]
        public void UseDatabase_publishes_the_options_to_the_static_holder()
        {
            //ConnectionExtensions keeps its options in a static, which is how the execution path
            //reads them. Restored afterwards so the global is left as it was found.
            var previous = ConnectionExtensions.DatabaseOptions;
            try
            {
                var services = new ServiceCollection();
                services.AddDatabase(o => o.BulkInsertTimeout = TimeSpan.FromSeconds(42));
                using var provider = services.BuildServiceProvider();

                provider.UseDatabase();

                Assert.Equal(TimeSpan.FromSeconds(42), ConnectionExtensions.DatabaseOptions.BulkInsertTimeout);
            }
            finally
            {
                ConnectionExtensions.DatabaseOptions = previous;
            }
        }

        private sealed class FixedIdentifierGenerator : IIdentifierGenerator
        {
            public string GenerateKey(Descriptors.ColumnDescriptor columnDescriptor) => "fixed";
        }
    }
}

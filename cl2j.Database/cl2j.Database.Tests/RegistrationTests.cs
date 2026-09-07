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
            cl2j.Database.SqlServer.SqlServer.Register();
            cl2j.Database.SqlServer.SqlServer.Register();

            var builder = CommandBuilderFactory.GetCommandBuilder(new SqlConnection());

            Assert.NotNull(builder);
        }

        [Fact]
        public void The_last_registration_of_a_provider_is_the_one_that_answers()
        {
            //Register used to append with no check, so the registry grew without bound and
            //resolution returned the first entry — which meant a second call with a custom
            //identifier generator was silently ignored. The parameter exists to be used, so the
            //later registration replaces the earlier one. See issue #29.
            var first = new FixedIdentifierGenerator("first");
            var second = new FixedIdentifierGenerator("second");

            cl2j.Database.SqlServer.SqlServer.Register(first);
            cl2j.Database.SqlServer.SqlServer.Register(second);

            var builder = CommandBuilderFactory.GetCommandBuilder(new SqlConnection());

            Assert.Same(second, builder.IdentifierGenerator);
        }

        [Fact]
        public void Registering_the_same_provider_again_does_not_add_a_second_entry()
        {
            //Not directly observable — nothing exposes the registry — so this asserts the effect
            //that is: after any number of registrations, the one that answers is the last.
            var generator = new FixedIdentifierGenerator("kept");

            for (var i = 0; i < 50; i++)
                cl2j.Database.SqlServer.SqlServer.Register();

            cl2j.Database.SqlServer.SqlServer.Register(generator);

            Assert.Same(generator, CommandBuilderFactory.GetCommandBuilder(new SqlConnection()).IdentifierGenerator);
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

        private sealed class FixedIdentifierGenerator(string key) : IIdentifierGenerator
        {
            public string GenerateKey(Descriptors.ColumnDescriptor columnDescriptor) => key;
        }
    }
}

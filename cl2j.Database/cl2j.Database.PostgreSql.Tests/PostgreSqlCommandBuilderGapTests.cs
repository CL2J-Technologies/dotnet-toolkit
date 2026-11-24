using cl2j.Database.CommandBuilders;
using cl2j.Database.Exceptions;
using cl2j.Database.Tests;
using Npgsql;
using Xunit;

namespace cl2j.Database.PostgreSql.Tests
{
    /// <summary>
    ///     What the builder does without a server: which connections it takes, and the ways the COPY
    ///     writer stops before it asks the server for anything.
    /// </summary>
    public class PostgreSqlCommandBuilderGapTests
    {
        private static ICommandBuilder Builder()
        {
            cl2j.Database.PostgreSql.PostgreSql.Register();
            return CommandBuilderFactory.GetCommandBuilder(new NpgsqlConnection());
        }

        [Fact]
        public void The_builder_is_resolved_for_an_Npgsql_connection_and_for_nothing_else()
        {
            cl2j.Database.PostgreSql.PostgreSql.Register();

            Assert.NotNull(CommandBuilderFactory.GetCommandBuilder(new NpgsqlConnection()));
            Assert.ThrowsAny<Exception>(() => CommandBuilderFactory.GetCommandBuilder(new UnsupportedConnection()));
        }

        [Fact]
        public async Task BulkInsert_refuses_a_connection_that_is_not_an_NpgsqlConnection()
        {
            using var connection = new UnsupportedConnection();

            var ex = await Assert.ThrowsAsync<DatabaseException>(
                () => Builder().BulkInsert(connection, new[] { new Customer { Id = "a", Name = "A" } }, CancellationToken.None));

            Assert.Equal("NpgsqlConnection required. 'UnsupportedConnection' received.", ex.Message);
        }

        [Fact]
        public async Task BulkInsert_fails_on_a_closed_connection_once_the_copy_is_prepared()
        {
            //No server needed: the rows are built and mapped before the copy starts, and the copy
            //itself is what refuses a connection that was never opened.
            using var connection = new NpgsqlConnection();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => Builder().BulkInsert(connection, new[] { new Customer { Id = "a", Name = "A" } }, CancellationToken.None));
        }

        [Fact]
        public async Task BulkInsert_of_nothing_does_not_touch_the_connection()
        {
            //A closed connection would refuse any statement: an empty batch has to finish before one.
            using var connection = new NpgsqlConnection();

            await Builder().BulkInsert(connection, Array.Empty<Customer>(), CancellationToken.None);
        }

        [Fact]
        public async Task BulkInsert_stops_on_a_token_that_is_already_cancelled_before_it_asks_the_server_for_anything()
        {
            //The closed connection would answer with InvalidOperationException if the copy were
            //started: seeing the cancellation instead proves the token is looked at first. That
            //matters on a real connection, where Npgsql breaks it when a token is cancelled in the
            //middle of the protocol exchange.
            using var connection = new NpgsqlConnection();
            using var cancelled = new CancellationTokenSource();
            await cancelled.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => Builder().BulkInsert(connection, new[] { new Customer { Id = "a", Name = "A" } }, cancelled.Token));
        }
    }
}

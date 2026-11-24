using cl2j.Database.Databases;
using cl2j.Database.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     <c>IDatabase</c> through <c>SqlServerDatabase</c>, built the way the README builds it.
    ///
    ///     <para>
    ///     This is the API consumers actually call. Every other class here goes through the
    ///     <c>DbConnection</c> extensions directly, which left the success path of
    ///     <c>BaseDatabase</c> — the open/call/return wrapper around each of them — unexecuted
    ///     against a server.
    ///     </para>
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class DatabaseFacadeTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        private readonly CapturingLogger logger = new();

        //Typed as the interface on purpose: the point is to call through IDatabase, not the class.
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "These tests exist to go through IDatabase.")]
        private IDatabase CreateDatabase() => new SqlServerDatabase(fixture.ConnectionString, new DatabaseOptions(), logger);

        public async Task InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(FacadeRow), CancellationToken.None);
            await connection.DropTableIfExists(typeof(FacadeIdentityRow), CancellationToken.None);
            await connection.CreateTable<FacadeRow>();
            await connection.CreateTable<FacadeIdentityRow>();
        }

        public async Task DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(FacadeRow), CancellationToken.None);
            await connection.DropTableIfExists(typeof(FacadeIdentityRow), CancellationToken.None);
        }

        private static async Task<FacadeRow> InsertRow(IDatabase database, string name, int quantity)
            => await database.Insert(new FacadeRow { Name = name, Quantity = quantity, Moment = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero) });

        [Fact]
        public async Task CreateConnection_hands_back_an_open_sql_connection()
        {
            var database = CreateDatabase();

            await using var connection = await database.CreateConnection();

            Assert.IsType<SqlConnection>(connection);
            Assert.Equal(System.Data.ConnectionState.Open, connection.State);
        }

        [Fact]
        public async Task Insert_fills_in_an_auto_generated_string_key_and_returns_the_entity()
        {
            var database = CreateDatabase();
            var row = new FacadeRow { Name = "first", Quantity = 3, Moment = DateTimeOffset.UtcNow };

            var returned = await database.Insert(row);

            Assert.Same(row, returned);
            Assert.StartsWith("fac_", row.Id, StringComparison.Ordinal);
            Assert.True(row.Id.Length > "fac_".Length);

            var read = await database.QuerySingle<FacadeRow>(new { row.Id });
            Assert.NotNull(read);
            Assert.Equal("first", read.Name);
            Assert.Equal(3, read.Quantity);
        }

        [Fact]
        public async Task Two_inserts_get_two_different_keys()
        {
            var database = CreateDatabase();

            var first = await InsertRow(database, "a", 1);
            var second = await InsertRow(database, "b", 2);

            Assert.NotEqual(first.Id, second.Id);
        }

        [Fact]
        public async Task Insert_leaves_an_identity_key_to_the_server()
        {
            var database = CreateDatabase();

            await database.Insert(new FacadeIdentityRow { Label = "server picks" });
            await database.Insert(new FacadeIdentityRow { Label = "again" });

            var rows = await database.Query<FacadeIdentityRow>();

            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.True(r.Id > 0));
            Assert.Equal(2, rows.Select(r => r.Id).Distinct().Count());
        }

        [Fact]
        public async Task Query_without_arguments_returns_every_row()
        {
            var database = CreateDatabase();
            await InsertRow(database, "a", 1);
            await InsertRow(database, "b", 2);
            await InsertRow(database, "c", 3);

            var rows = await database.Query<FacadeRow>();

            Assert.Equal(["a", "b", "c"], rows.Select(r => r.Name).Order());
        }

        [Fact]
        public async Task Query_of_an_empty_table_is_an_empty_list()
        {
            var database = CreateDatabase();

            Assert.Empty(await database.Query<FacadeRow>());
        }

        [Fact]
        public async Task Query_by_example_filters_on_the_properties_of_the_argument()
        {
            var database = CreateDatabase();
            await InsertRow(database, "a", 1);
            await InsertRow(database, "b", 2);
            await InsertRow(database, "b", 3);

            var rows = await database.Query<FacadeRow>(new { Name = "b" });

            Assert.Equal([2, 3], rows.Select(r => r.Quantity).Order());
        }

        [Fact]
        public async Task Query_by_example_with_a_null_argument_returns_everything()
        {
            var database = CreateDatabase();
            await InsertRow(database, "a", 1);
            await InsertRow(database, "b", 2);

            var rows = await database.Query<FacadeRow>((object?)null);

            Assert.Equal(2, rows.Count);
        }

        [Fact]
        public async Task Query_with_a_statement_binds_the_parameters()
        {
            var database = CreateDatabase();
            await InsertRow(database, "a", 1);
            await InsertRow(database, "b", 2);
            await InsertRow(database, "c", 3);

            var rows = await database.Query<FacadeRow>(
                await database.BuildSelectStatement<FacadeRow>("WHERE [Quantity] >= @Quantity"),
                new { Quantity = 2 });

            Assert.Equal(["b", "c"], rows.Select(r => r.Name).Order());
        }

        [Fact]
        public async Task Query_with_a_statement_and_no_parameters_runs_as_written()
        {
            var database = CreateDatabase();
            await InsertRow(database, "a", 1);

            var rows = await database.Query<FacadeRow>("SELECT [Id],[Name],[Quantity],[Moment] FROM [Facade]");

            Assert.Single(rows);
        }

        [Fact]
        public async Task Query_with_a_statement_traces_the_row_count()
        {
            var database = CreateDatabase();
            await InsertRow(database, "a", 1);
            await InsertRow(database, "b", 2);

            await database.Query<FacadeRow>("SELECT [Id],[Name],[Quantity],[Moment] FROM [Facade]");

            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Trace && e.Message.Contains("-> 2", StringComparison.Ordinal));
        }

        [Fact]
        public async Task BuildSelectStatement_is_the_select_alone_or_followed_by_the_suffix()
        {
            var database = CreateDatabase();

            var plain = await database.BuildSelectStatement<FacadeRow>();
            var filtered = await database.BuildSelectStatement<FacadeRow>("WHERE [Quantity] = 1");

            Assert.StartsWith("SELECT ", plain, StringComparison.Ordinal);
            Assert.Contains("[Facade]", plain, StringComparison.Ordinal);
            Assert.Equal(plain + " WHERE [Quantity] = 1", filtered);
        }

        [Fact]
        public async Task QuerySingle_with_a_statement_returns_the_matching_row()
        {
            var database = CreateDatabase();
            await InsertRow(database, "a", 1);
            var wanted = await InsertRow(database, "b", 2);

            var read = await database.QuerySingle<FacadeRow>(
                await database.BuildSelectStatement<FacadeRow>("WHERE [Name] = @Name"),
                new { Name = "b" });

            Assert.NotNull(read);
            Assert.Equal(wanted.Id, read.Id);
        }

        [Fact]
        public async Task QuerySingle_with_a_statement_and_no_match_is_null()
        {
            var database = CreateDatabase();

            var read = await database.QuerySingle<FacadeRow>(await database.BuildSelectStatement<FacadeRow>());

            Assert.Null(read);
        }

        [Fact]
        public async Task QuerySingle_by_example_returns_the_row_with_that_key()
        {
            var database = CreateDatabase();
            await InsertRow(database, "a", 1);
            var wanted = await InsertRow(database, "b", 2);
            await InsertRow(database, "c", 3);

            var read = await database.QuerySingle<FacadeRow>(new { wanted.Id });

            Assert.NotNull(read);
            Assert.Equal("b", read.Name);
            Assert.Equal(2, read.Quantity);
            Assert.Equal(wanted.Moment, read.Moment);
        }

        [Fact]
        public async Task QuerySingle_by_example_with_no_match_is_null()
        {
            var database = CreateDatabase();
            await InsertRow(database, "a", 1);

            Assert.Null(await database.QuerySingle<FacadeRow>(new { Id = "fac_missing" }));
        }

        [Fact]
        public async Task QueryKeys_returns_exactly_the_rows_asked_for()
        {
            var database = CreateDatabase();
            var a = await InsertRow(database, "a", 1);
            await InsertRow(database, "b", 2);
            var c = await InsertRow(database, "c", 3);

            var rows = await database.QueryKeys<FacadeRow>([a.Id, c.Id, "fac_missing"]);

            Assert.Equal(["a", "c"], rows.Select(r => r.Name).Order());
        }

        [Fact]
        public async Task QueryKeys_with_no_keys_is_an_empty_list()
        {
            var database = CreateDatabase();
            await InsertRow(database, "a", 1);

            Assert.Empty(await database.QueryKeys<FacadeRow>([]));
        }

        [Fact]
        public async Task Exists_tells_a_present_row_from_an_absent_one()
        {
            var database = CreateDatabase();
            var row = await InsertRow(database, "a", 1);

            Assert.True(await database.Exists<FacadeRow>(new { row.Id }));
            Assert.False(await database.Exists<FacadeRow>(new { Id = "fac_missing" }));
        }

        [Fact]
        public async Task NewKey_gives_a_fresh_value_for_a_self_generated_key()
        {
            var database = CreateDatabase();

            var first = await database.NewKey<RoundTripRow>();
            var second = await database.NewKey<RoundTripRow>();

            Assert.False(string.IsNullOrEmpty(first));
            Assert.NotEqual(first, second);
        }

        [Fact]
        public async Task Update_writes_every_column_and_returns_the_entity()
        {
            var database = CreateDatabase();
            var row = await InsertRow(database, "before", 1);
            var other = await InsertRow(database, "untouched", 9);

            row.Name = "after";
            row.Quantity = 42;
            var returned = await database.Update(row);

            Assert.Same(row, returned);
            var read = await database.QuerySingle<FacadeRow>(new { row.Id });
            Assert.Equal("after", read!.Name);
            Assert.Equal(42, read.Quantity);

            var untouched = await database.QuerySingle<FacadeRow>(new { other.Id });
            Assert.Equal("untouched", untouched!.Name);
            Assert.Equal(9, untouched.Quantity);
        }

        [Fact]
        public async Task Update_of_one_column_leaves_the_others_alone()
        {
            var database = CreateDatabase();
            var row = await InsertRow(database, "kept", 1);

            await database.Update(row, "Quantity", 77);

            var read = await database.QuerySingle<FacadeRow>(new { row.Id });
            Assert.Equal(77, read!.Quantity);
            Assert.Equal("kept", read.Name);
        }

        [Fact]
        public async Task DeleteKey_removes_that_row_and_only_that_row()
        {
            var database = CreateDatabase();
            var doomed = await InsertRow(database, "doomed", 1);
            var survivor = await InsertRow(database, "survivor", 2);

            await database.DeleteKey<FacadeRow>(doomed.Id);

            var rows = await database.Query<FacadeRow>();
            var left = Assert.Single(rows);
            Assert.Equal(survivor.Id, left.Id);
        }

        [Fact]
        public async Task DeleteKey_of_a_key_that_is_not_there_is_not_an_error()
        {
            var database = CreateDatabase();
            await InsertRow(database, "a", 1);

            await database.DeleteKey<FacadeRow>("fac_missing");

            Assert.Single(await database.Query<FacadeRow>());
        }

        [Fact]
        public async Task Execute_reports_the_rows_it_touched()
        {
            var database = CreateDatabase();
            await InsertRow(database, "a", 1);
            await InsertRow(database, "b", 5);
            await InsertRow(database, "c", 5);

            var touched = await database.Execute("UPDATE [Facade] SET [Name] = 'five' WHERE [Quantity] = @Quantity", new { Quantity = 5 });

            Assert.Equal(2, touched);
            Assert.Equal(2, (await database.Query<FacadeRow>(new { Name = "five" })).Count);
        }

        [Fact]
        public async Task Execute_without_parameters_runs_the_statement_as_written()
        {
            var database = CreateDatabase();
            await InsertRow(database, "a", 1);
            await InsertRow(database, "b", 2);

            var touched = await database.Execute("DELETE FROM [Facade]");

            Assert.Equal(2, touched);
            Assert.Empty(await database.Query<FacadeRow>());
        }

        [Fact]
        public async Task A_failing_statement_is_logged_at_the_exception_level_and_rethrown()
        {
            var database = new SqlServerDatabase(fixture.ConnectionString, new DatabaseOptions { ExceptionLevel = LogLevel.Error }, logger);

            await Assert.ThrowsAsync<SqlException>(() => database.Execute("SELECT * FROM [NoSuchTable]"));

            var entry = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.NotNull(entry.Exception);
        }

        [Fact]
        public async Task A_round_trip_of_the_whole_lifecycle_ends_with_an_empty_table()
        {
            var database = CreateDatabase();

            var row = await database.Insert(new FacadeRow { Name = "life", Quantity = 1, Moment = DateTimeOffset.UtcNow });
            Assert.True(await database.Exists<FacadeRow>(new { row.Id }));

            row.Quantity = 2;
            await database.Update(row);
            Assert.Equal(2, (await database.QueryKeys<FacadeRow>([row.Id])).Single().Quantity);

            await database.DeleteKey<FacadeRow>(row.Id);
            Assert.False(await database.Exists<FacadeRow>(new { row.Id }));
            Assert.Empty(await database.Query<FacadeRow>());
        }

        private sealed class CapturingLogger : ILogger
        {
            public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}

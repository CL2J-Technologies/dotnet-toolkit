using System.Data;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     The end-to-end check for issue #7, and the one thing the unit tests could not do.
    ///
    ///     <para>
    ///     Those assert which <c>DbType</c> the library chooses. They cannot assert what SQL Server
    ///     then does with it — and that is the entire claim: a Unicode parameter compared against a
    ///     <c>varchar</c> column makes the server convert <b>the column</b>, and a converted column
    ///     cannot be seeked. The plan shows that as <c>CONVERT_IMPLICIT</c> in the predicate.
    ///     </para>
    ///
    ///     <para>
    ///     There is a negative control here on purpose. An assertion that something is absent is
    ///     worthless unless the same check can be shown to find it when it is there, so the second
    ///     test runs the identical query with an <c>nvarchar</c> parameter and requires the
    ///     conversion to appear.
    ///     </para>
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class ParameterTypingPlanTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(AnsiKeyedRow), CancellationToken.None);
            await connection.CreateTable<AnsiKeyedRow>();

            for (var i = 0; i < 20; i++)
                await connection.Insert(new AnsiKeyedRow { Code = $"code-{i:D3}", Label = $"label {i}" });
        }

        public async Task DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(AnsiKeyedRow), CancellationToken.None);
        }

        /// <summary>
        ///     Confirms the column really is <c>varchar</c>. Everything below rests on it: against
        ///     an <c>nvarchar</c> column there would be nothing to convert and both tests would
        ///     pass for the wrong reason.
        /// </summary>
        [Fact]
        public async Task The_key_column_is_varchar()
        {
            await using var connection = await fixture.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'AnsiKeyed' AND COLUMN_NAME = 'Code'
                """;

            var dataType = (string?)await command.ExecuteScalarAsync();

            Assert.Equal("varchar", dataType);
        }

        [Fact]
        public async Task A_key_lookup_through_the_library_does_not_convert_the_column()
        {
            await using var connection = await fixture.OpenAsync();
            await ClearPlanCacheAsync(connection);

            await connection.QueryKey<AnsiKeyedRow>("code-007");

            var plan = await FindPlanAsync(connection, "AnsiKeyed");

            Assert.NotNull(plan);
            Assert.DoesNotContain("CONVERT_IMPLICIT", plan, StringComparison.Ordinal);
        }

        [Fact]
        public async Task The_same_query_with_a_unicode_parameter_does_convert_the_column()
        {
            //The negative control. Same table, same predicate, only the parameter type differs.
            //If this does not find CONVERT_IMPLICIT, the test above proves nothing.
            await using var connection = await fixture.OpenAsync();
            await ClearPlanCacheAsync(connection);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT [Code],[Label] FROM [AnsiKeyed] WHERE [Code]=@Code";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@Code";
                parameter.DbType = DbType.String;
                parameter.Value = "code-007";
                command.Parameters.Add(parameter);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { }
            }

            var plan = await FindPlanAsync(connection, "AnsiKeyed");

            Assert.NotNull(plan);
            Assert.Contains("CONVERT_IMPLICIT", plan, StringComparison.Ordinal);
        }

        private static async Task ClearPlanCacheAsync(System.Data.Common.DbConnection connection)
        {
            //A container of our own, so emptying the cache costs nothing and keeps the lookup below
            //from finding a plan left by another test.
            await using var command = connection.CreateCommand();
            command.CommandText = "DBCC FREEPROCCACHE WITH NO_INFOMSGS";
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<string?> FindPlanAsync(System.Data.Common.DbConnection connection, string mustMention)
        {
            await using var command = connection.CreateCommand();
            //No brackets in the pattern: in T-SQL LIKE, [...] is a character class, so a pattern
            //written as %[AnsiKeyed]% matches any string containing any one of those letters —
            //which is to say almost everything. That mistake made this lookup return an unrelated
            //plan whose XML happened to be NULL, and the test failed for a reason that had nothing
            //to do with what it was checking.
            //
            //query_plan IS NOT NULL because sys.dm_exec_query_plan returns no XML for some cached
            //entries, and a null plan would read as "no conversion found".
            command.CommandText = """
                SELECT TOP 1 CAST(qp.query_plan AS nvarchar(max))
                FROM sys.dm_exec_cached_plans cp
                CROSS APPLY sys.dm_exec_query_plan(cp.plan_handle) qp
                CROSS APPLY sys.dm_exec_sql_text(cp.plan_handle) st
                WHERE st.text LIKE @Pattern
                  AND st.text NOT LIKE '%dm_exec_cached_plans%'
                  AND qp.query_plan IS NOT NULL
                """;

            var parameter = (SqlParameter)command.CreateParameter();
            parameter.ParameterName = "@Pattern";
            parameter.Value = "%" + mustMention + "%";
            command.Parameters.Add(parameter);

            return await command.ExecuteScalarAsync() as string;
        }
    }
}

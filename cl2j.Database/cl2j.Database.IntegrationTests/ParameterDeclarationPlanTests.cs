using System.Data.Common;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     What the unit tests cannot show: that SQL Server, given the fixed declarations, really
    ///     keeps one plan per query text — and that nothing about a value is lost on the way.
    ///
    ///     <para>
    ///     The plan cache is the witness. A batch sent through <c>sp_executesql</c> is cached under
    ///     its text, parameter declarations included, so two executions that differ only in how
    ///     long a string was used to leave two entries. Each test tags its statement with a comment
    ///     of its own and counts the entries that carry the tag.
    ///     </para>
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class ParameterDeclarationPlanTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.Execute("""
                IF OBJECT_ID('DecimalSeek') IS NOT NULL DROP TABLE [DecimalSeek];
                CREATE TABLE [DecimalSeek] ([Id] int IDENTITY PRIMARY KEY, [Lat] decimal(9,6) NOT NULL);
                INSERT INTO [DecimalSeek] ([Lat])
                SELECT TOP (20000) CAST(-90 + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) * 0.009 AS decimal(9,6))
                FROM sys.all_objects a CROSS JOIN sys.all_objects b;
                CREATE INDEX [IX_DecimalSeek_Lat] ON [DecimalSeek] ([Lat]);
                """);
        }

        public async Task DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.Execute("IF OBJECT_ID('DecimalSeek') IS NOT NULL DROP TABLE [DecimalSeek];");
        }

        [Fact]
        public async Task Strings_of_different_lengths_share_one_plan()
        {
            var sql = Tagged("SELECT 1 AS [Value] WHERE @Name IS NULL OR @Name <> ''", out var tag);
            await using var connection = await fixture.OpenAsync();

            await connection.Query<ValueRow>(sql, new { Name = "abc" });
            await connection.Query<ValueRow>(sql, new { Name = "abcdefghij" });

            Assert.Equal(1, await CachedPlans(connection, tag));
        }

        [Fact]
        public async Task Decimals_of_different_scales_share_one_plan()
        {
            var sql = Tagged("SELECT 1 AS [Value] WHERE @Lat IS NULL OR @Lat > 0", out var tag);
            await using var connection = await fixture.OpenAsync();

            await connection.Query<ValueRow>(sql, new { Lat = 45.5m });
            await connection.Query<ValueRow>(sql, new { Lat = 45.123456789012m });

            Assert.Equal(1, await CachedPlans(connection, tag));
        }

        [Fact]
        public async Task A_null_and_a_value_share_one_plan()
        {
            var sql = Tagged("SELECT 1 AS [Value] WHERE @When IS NULL OR @When > '2000-01-01'", out var tag);
            await using var connection = await fixture.OpenAsync();

            await connection.Query<ValueRow>(sql, new { When = (DateTimeOffset?)null });
            await connection.Query<ValueRow>(sql, new { When = (DateTimeOffset?)DateTimeOffset.UtcNow });

            Assert.Equal(1, await CachedPlans(connection, tag));
        }

        [Theory]
        [InlineData(4000)]
        [InlineData(4001)]
        [InlineData(9000)]
        public async Task A_string_comes_back_whole_on_either_side_of_the_common_size(int length)
        {
            //Non-ASCII on purpose: it is what an nvarchar has to carry, and what a wrong binding
            //would turn into question marks.
            var text = string.Concat(Enumerable.Repeat("é", length));
            await using var connection = await fixture.OpenAsync();

            var rows = await connection.Query<TextRow>("SELECT @Text AS [Text]", new { Text = text });

            Assert.Equal(text, Assert.Single(rows).Text);
        }

        [Fact]
        public async Task A_decimal_that_does_not_fit_the_fixed_declaration_comes_back_exact()
        {
            var value = 0.1234567890123456789012345m;
            await using var connection = await fixture.OpenAsync();

            var rows = await connection.Query<AmountRow>("SELECT @Amount AS [Amount]", new { Amount = value });

            Assert.Equal(value, Assert.Single(rows).Amount);
        }

        [Fact]
        public async Task A_narrow_decimal_column_is_still_sought_through_the_wide_parameter()
        {
            //decimal(38,18) against decimal(9,6): wider on both sides. If SQL Server had to convert
            //the column to compare them, the index would stop being seekable — the failure the
            //recommendation query this was measured on cannot afford.
            var sql = Tagged("SELECT COUNT(*) AS [Value] FROM [DecimalSeek] WHERE [Lat] BETWEEN @LatMin AND @LatMax", out var tag);
            await using var connection = await fixture.OpenAsync();

            var rows = await connection.Query<ValueRow>(sql, new { LatMin = 45.5m, LatMax = 45.6m });
            var plan = await CachedPlan(connection, tag);

            Assert.True(Assert.Single(rows).Value > 0);
            Assert.NotNull(plan);
            Assert.Contains("PhysicalOp=\"Index Seek\"", plan, StringComparison.Ordinal);
            Assert.Contains("[IX_DecimalSeek_Lat]", plan, StringComparison.Ordinal);
        }

        //A comment of its own, so the cache lookup below cannot find another test's statement.
        private static string Tagged(string sql, out string tag)
        {
            tag = "declaration_" + Guid.NewGuid().ToString("N");
            return $"{sql} /* {tag} */";
        }

        private static async Task<int> CachedPlans(DbConnection connection, string tag)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sys.dm_exec_cached_plans cp
                CROSS APPLY sys.dm_exec_sql_text(cp.plan_handle) st
                WHERE st.text LIKE @Pattern AND st.text NOT LIKE '%dm_exec_cached_plans%'
                """;
            var pattern = command.CreateParameter();
            pattern.ParameterName = "@Pattern";
            pattern.Value = "%" + tag + "%";
            command.Parameters.Add(pattern);
            return (int)(await command.ExecuteScalarAsync())!;
        }

        private static async Task<string?> CachedPlan(DbConnection connection, string tag)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP 1 CAST(qp.query_plan AS nvarchar(max))
                FROM sys.dm_exec_cached_plans cp
                CROSS APPLY sys.dm_exec_query_plan(cp.plan_handle) qp
                CROSS APPLY sys.dm_exec_sql_text(cp.plan_handle) st
                WHERE st.text LIKE @Pattern AND st.text NOT LIKE '%dm_exec_cached_plans%' AND qp.query_plan IS NOT NULL
                """;
            var pattern = command.CreateParameter();
            pattern.ParameterName = "@Pattern";
            pattern.Value = "%" + tag + "%";
            command.Parameters.Add(pattern);
            return await command.ExecuteScalarAsync() as string;
        }
    }

    public sealed class ValueRow
    {
        public int Value { get; set; }
    }

    public sealed class TextRow
    {
        public string Text { get; set; } = string.Empty;
    }

    public sealed class AmountRow
    {
        public decimal Amount { get; set; }
    }
}

using cl2j.Database.CommandBuilders;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     A read-by-key statement with no WHERE clause raises nothing: it returns the first row of
    ///     the table, for whatever key was asked for. That is the most expensive shape of failure —
    ///     a valid entity, but the wrong one.
    /// </summary>
    public class QueryByKeyStatementTests
    {
        private static ICommandBuilder Builder()
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            return CommandBuilderFactory.GetCommandBuilder(new SqlConnection());
        }

        [Fact]
        public void The_statement_filters_on_the_key_column()
        {
            var statement = Builder().GetQueryByKeyStatement(typeof(Customer));

            Assert.EndsWith(" WHERE [Id]=@Id", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Several_key_columns_are_joined_with_AND()
        {
            var statement = Builder().GetQueryByKeyStatement(typeof(TenantScopedRow));

            Assert.EndsWith(" WHERE [TenantId]=@TenantId AND [Id]=@Id", statement.Text, StringComparison.Ordinal);
        }
    }
}

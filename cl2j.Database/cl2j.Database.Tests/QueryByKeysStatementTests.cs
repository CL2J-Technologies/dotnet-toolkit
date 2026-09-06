using cl2j.Database.CommandBuilders;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     These tests touch no database. They build the statement and look at what it contains —
    ///     the only thing that matters here: where the key values end up.
    /// </summary>
    public class QueryByKeysStatementTests
    {
        private static ICommandBuilder Builder()
        {
            //Register appends to a static list. GetCommandBuilder returns the first builder that
            //supports the connection, so re-registering between tests has no side effect.
            cl2j.Database.SqlServer.SqlServer.Register();
            return CommandBuilderFactory.GetCommandBuilder(new SqlConnection());
        }

        [Fact]
        public void Key_values_never_appear_in_the_statement_text()
        {
            var statement = Builder().GetQueryByKeysStatement(typeof(Customer), ["cust-1", "cust-2"]);

            Assert.DoesNotContain("cust-1", statement.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("cust-2", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void The_in_clause_lists_one_placeholder_per_key()
        {
            var statement = Builder().GetQueryByKeysStatement(typeof(Customer), ["a", "b"]);

            Assert.EndsWith("WHERE [Id] IN (@key0,@key1) ", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Every_key_is_carried_as_a_bound_parameter()
        {
            var statement = Builder().GetQueryByKeysStatement(typeof(Customer), ["cust-1", "cust-2", "cust-3"]);

            Assert.Equal(3, statement.Parameters.Count);
            Assert.Equal(["cust-1", "cust-2", "cust-3"], statement.Parameters.Select(p => p.Value));

            //Every name must appear in the text, otherwise the parameter is bound to nothing.
            foreach (var parameter in statement.Parameters)
                Assert.Contains("@" + parameter.Name, statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Each_key_parameter_carries_the_column_it_is_compared_against()
        {
            //Without the column, binding has nothing to type the value from, and the keys of an IN
            //list would go out as Unicode against an ANSI key column — issue #7 on a second path.
            var statement = Builder().GetQueryByKeysStatement(typeof(Customer), ["cust-1", "cust-2"]);

            Assert.All(statement.Parameters, p => Assert.Equal("Id", p.Column?.Name));
        }

        [Fact]
        public void A_quote_in_a_key_does_not_reach_the_statement_text()
        {
            var statement = Builder().GetQueryByKeysStatement(typeof(Customer), ["O'Brien"]);

            Assert.DoesNotContain("'", statement.Text, StringComparison.Ordinal);
        }
    }
}

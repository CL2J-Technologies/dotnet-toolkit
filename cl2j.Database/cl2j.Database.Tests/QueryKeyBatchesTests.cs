using cl2j.Database.CommandBuilders;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     SQL Server refuses a command beyond 2100 parameters. A long enough key list must
    ///     therefore become several statements — without a key being lost or repeated along the
    ///     way, which is the only way a split can lie silently.
    /// </summary>
    public class QueryKeyBatchesTests
    {
        private static ICommandBuilder Builder()
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            return CommandBuilderFactory.GetCommandBuilder(new SqlConnection());
        }

        private static object[] Keys(int count)
            => [.. Enumerable.Range(0, count).Select(i => (object)$"cust-{i}")];

        [Fact]
        public void No_statement_carries_more_parameters_than_the_server_accepts()
        {
            var statements = QueryKeyBatches.Build(Builder(), typeof(Customer), Keys(5000)).ToList();

            Assert.NotEmpty(statements);
            Assert.All(statements, s => Assert.InRange(s.Parameters.Count, 1, QueryKeyBatches.MaxKeysPerStatement));
        }

        [Fact]
        public void Splitting_keeps_every_key_exactly_once_and_in_order()
        {
            var keys = Keys(5000);

            var statements = QueryKeyBatches.Build(Builder(), typeof(Customer), keys).ToList();

            var carried = statements.SelectMany(s => s.Parameters).Select(p => p.Value).ToList();
            Assert.Equal(keys, carried);
        }

        [Fact]
        public void A_count_that_lands_on_the_batch_size_does_not_produce_a_trailing_empty_statement()
        {
            var statements = QueryKeyBatches.Build(Builder(), typeof(Customer), Keys(QueryKeyBatches.MaxKeysPerStatement)).ToList();

            Assert.Single(statements);
        }

        [Fact]
        public void No_keys_produces_no_statement()
        {
            //An empty IN is not valid SQL. Producing nothing is the only correct way out.
            var statements = QueryKeyBatches.Build(Builder(), typeof(Customer), []).ToList();

            Assert.Empty(statements);
        }
    }
}

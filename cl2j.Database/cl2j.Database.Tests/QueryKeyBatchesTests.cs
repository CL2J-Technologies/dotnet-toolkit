using cl2j.Database.CommandBuilders;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     SQL Server refuse une commande au-dela de 2100 parametres. Une liste de cles assez
    ///     longue doit donc devenir plusieurs enonces — sans qu aucune cle ne se perde ni ne se
    ///     repete au passage, ce qui est la seule facon dont un decoupage peut mentir en silence.
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
            //Un IN vide n est pas du SQL valide. Ne rien produire est la seule sortie correcte.
            var statements = QueryKeyBatches.Build(Builder(), typeof(Customer), []).ToList();

            Assert.Empty(statements);
        }
    }
}

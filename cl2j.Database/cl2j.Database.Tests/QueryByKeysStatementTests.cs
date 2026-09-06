using cl2j.Database.CommandBuilders;
using cl2j.Database.SqlServer;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     Ces tests ne touchent aucune base. Ils construisent l enonce et regardent ce qu il
    ///     contient — c est la seule chose qui compte ici : ou finissent les valeurs de cle.
    /// </summary>
    public class QueryByKeysStatementTests
    {
        private static ICommandBuilder Builder()
        {
            //Register empile dans une liste statique. GetCommandBuilder rend le premier qui supporte
            //la connexion, donc reenregistrer entre les tests est sans effet de bord.
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

            //Chaque nom doit apparaitre dans le texte, sinon le parametre est lie a rien.
            foreach (var parameter in statement.Parameters)
                Assert.Contains("@" + parameter.Name, statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void A_quote_in_a_key_does_not_reach_the_statement_text()
        {
            var statement = Builder().GetQueryByKeysStatement(typeof(Customer), ["O'Brien"]);

            Assert.DoesNotContain("'", statement.Text, StringComparison.Ordinal);
        }
    }
}

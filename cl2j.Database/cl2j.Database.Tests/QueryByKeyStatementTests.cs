using cl2j.Database.CommandBuilders;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     Un enonce de lecture par cle qui n a pas de clause WHERE ne leve rien : il rend la
    ///     premiere ligne de la table, pour n importe quelle cle demandee. C est la forme d erreur
    ///     la plus couteuse — une entite valide, mais la mauvaise.
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

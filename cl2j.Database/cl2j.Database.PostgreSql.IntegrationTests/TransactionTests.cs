using Xunit;

namespace cl2j.Database.PostgreSql.IntegrationTests
{
    /// <summary>PostgreSQL's run of the shared <see cref="cl2j.Database.IntegrationTests.TransactionTestsBase"/>. Nothing differs, so nothing is overridden.</summary>
    [Collection(PostgreSqlGroup.Name)]
    public class TransactionTests(PostgreSqlFixture fixture) : cl2j.Database.IntegrationTests.TransactionTestsBase(fixture)
    {
    }
}

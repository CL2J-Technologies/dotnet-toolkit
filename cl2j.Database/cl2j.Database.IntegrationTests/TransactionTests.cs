using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     SQL Server's run of the shared <see cref="TransactionTestsBase"/>. Every default in the base
    ///     is SQL Server's own value, so nothing is overridden.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class TransactionTests(SqlServerFixture fixture) : TransactionTestsBase(fixture)
    {
    }
}

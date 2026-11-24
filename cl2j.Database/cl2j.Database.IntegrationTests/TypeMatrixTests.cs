using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     SQL Server's run of the shared <see cref="TypeMatrixTestsBase"/>. Every default in the
    ///     base is SQL Server's own value, so nothing is overridden.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class TypeMatrixTests(SqlServerFixture fixture) : TypeMatrixTestsBase(fixture)
    {
    }
}

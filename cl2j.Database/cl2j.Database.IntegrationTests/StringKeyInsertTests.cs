using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     SQL Server's run of the shared <see cref="StringKeyInsertTestsBase"/>. Every default in the base
    ///     is SQL Server's own value, so nothing is overridden.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class StringKeyInsertTests(SqlServerFixture fixture) : StringKeyInsertTestsBase(fixture)
    {
    }
}

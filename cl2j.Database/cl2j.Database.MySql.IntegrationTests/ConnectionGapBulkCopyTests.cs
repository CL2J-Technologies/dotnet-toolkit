using cl2j.Database.IntegrationTests;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    /// <summary>
    ///     MySQL's run of the shared <see cref="cl2j.Database.IntegrationTests.ConnectionGapTestsBase"/> over MySqlBulkCopy (the connection string allows LOAD DATA LOCAL INFILE).
    /// </summary>
    [Collection(MySqlTestGroup.Name)]
    public class ConnectionGapBulkCopyTests(MySqlFixture fixture) : ConnectionGapTestsBase(fixture.BulkCopy)
    {
    }
}

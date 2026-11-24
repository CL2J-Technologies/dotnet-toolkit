using cl2j.Database.IntegrationTests;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    /// <summary>
    ///     MySQL's run of the shared <see cref="cl2j.Database.IntegrationTests.BulkAndBatchTestsBase"/>, with MySqlBulkCopy. 2500 rows cross the 1000-row INSERT batch twice.
    /// </summary>
    [Collection(MySqlTestGroup.Name)]
    public class BulkAndBatchBulkCopyTests(MySqlFixture fixture) : BulkAndBatchTestsBase(fixture.BulkCopy)
    {
        protected override int BulkRowCount => 2500;
    }
}

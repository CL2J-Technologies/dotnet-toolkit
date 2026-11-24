using Xunit;

namespace cl2j.Database.PostgreSql.IntegrationTests
{
    /// <summary>PostgreSQL's run of the shared <see cref="cl2j.Database.IntegrationTests.BulkAndBatchTestsBase"/>.</summary>
    [Collection(PostgreSqlGroup.Name)]
    public class BulkAndBatchTests(PostgreSqlFixture fixture) : cl2j.Database.IntegrationTests.BulkAndBatchTestsBase(fixture)
    {
        //The defaults hold: 2,500 keys is above MaxKeysPerStatement (2,000), so the split is exercised.
    }
}

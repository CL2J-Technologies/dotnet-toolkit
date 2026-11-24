using Xunit;

namespace cl2j.Database.PostgreSql.IntegrationTests
{
    /// <summary>PostgreSQL's run of the shared <see cref="cl2j.Database.IntegrationTests.KnownDefectTestsBase"/>. Nothing differs, so nothing is overridden.</summary>
    [Collection(PostgreSqlGroup.Name)]
    public class KnownDefectTests(PostgreSqlFixture fixture) : cl2j.Database.IntegrationTests.KnownDefectTestsBase(fixture)
    {
    }
}

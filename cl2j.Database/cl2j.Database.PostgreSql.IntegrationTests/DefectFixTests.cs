using cl2j.Database.IntegrationTests;
using Xunit;

namespace cl2j.Database.PostgreSql.IntegrationTests
{
    /// <summary>PostgreSQL's run of the shared <see cref="DefectFixTestsBase"/>. Nothing differs, so nothing is overridden.</summary>
    [Collection(PostgreSqlGroup.Name)]
    public class DefectFixTests(PostgreSqlFixture fixture) : DefectFixTestsBase(fixture)
    {
    }
}

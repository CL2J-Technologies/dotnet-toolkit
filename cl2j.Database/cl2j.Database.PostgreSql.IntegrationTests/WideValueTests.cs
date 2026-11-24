using Xunit;

namespace cl2j.Database.PostgreSql.IntegrationTests
{
    /// <summary>PostgreSQL's run of the shared <see cref="cl2j.Database.IntegrationTests.WideValueTestsBase"/>.</summary>
    [Collection(PostgreSqlGroup.Name)]
    public class WideValueTests(PostgreSqlFixture fixture) : cl2j.Database.IntegrationTests.WideValueTestsBase(fixture)
    {
        protected override string LongTypeName => "bigint";

        protected override string DoubleTypeName => "double precision";

        protected override string GuidTypeName => "uuid";
    }
}

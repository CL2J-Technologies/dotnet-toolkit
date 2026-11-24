using Xunit;

namespace cl2j.Database.PostgreSql.IntegrationTests
{
    /// <summary>PostgreSQL's run of the shared <see cref="cl2j.Database.IntegrationTests.NullableColumnTestsBase"/>.</summary>
    [Collection(PostgreSqlGroup.Name)]
    public class NullableColumnTests(PostgreSqlFixture fixture) : cl2j.Database.IntegrationTests.NullableColumnTestsBase(fixture)
    {
        protected override string IntTypeName => "integer";

        protected override string DateTimeTypeName => "timestamp without time zone";

        protected override string GuidTypeName => "uuid";
    }
}

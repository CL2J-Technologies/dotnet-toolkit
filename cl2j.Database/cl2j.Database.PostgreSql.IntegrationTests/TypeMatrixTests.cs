using Xunit;

namespace cl2j.Database.PostgreSql.IntegrationTests
{
    /// <summary>PostgreSQL's run of the shared <see cref="cl2j.Database.IntegrationTests.TypeMatrixTestsBase"/>.</summary>
    [Collection(PostgreSqlGroup.Name)]
    public class TypeMatrixTests(PostgreSqlFixture fixture) : cl2j.Database.IntegrationTests.TypeMatrixTestsBase(fixture)
    {
        protected override int TemporalFractionalDigits => PostgreSqlTypeHooks.TemporalFractionalDigits;

        protected override object ExpectedValue(object written) => PostgreSqlTypeHooks.ExpectedValue(written);

        protected override string ExpectedColumnType(Type clrType) => PostgreSqlTypeHooks.ExpectedColumnType(clrType);
    }
}

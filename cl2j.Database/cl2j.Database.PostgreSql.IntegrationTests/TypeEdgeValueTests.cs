using Xunit;

namespace cl2j.Database.PostgreSql.IntegrationTests
{
    /// <summary>PostgreSQL's run of the shared <see cref="cl2j.Database.IntegrationTests.TypeEdgeValueTestsBase"/>.</summary>
    [Collection(PostgreSqlGroup.Name)]
    public class TypeEdgeValueTests(PostgreSqlFixture fixture) : cl2j.Database.IntegrationTests.TypeEdgeValueTestsBase(fixture)
    {
        protected override int TemporalFractionalDigits => PostgreSqlTypeHooks.TemporalFractionalDigits;

        protected override object ExpectedValue(object written) => PostgreSqlTypeHooks.ExpectedValue(written);

        //double precision stores NaN and both infinities.
        protected override bool StoresNonFiniteDoubles => true;

        //interval is a duration, not a time of day.
        protected override bool RefusesTimeSpanOutsideADay => false;
    }
}

using cl2j.Database.IntegrationTests;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    /// <summary>
    ///     MySQL's run of the shared <see cref="cl2j.Database.IntegrationTests.TypeEdgeValueTestsBase"/>. The hooks are MySQL's: six fractional digits, an instant instead of an offset.
    /// </summary>
    [Collection(MySqlTestGroup.Name)]
    public class TypeEdgeValueBulkCopyTests(MySqlFixture fixture) : TypeEdgeValueTestsBase(fixture.BulkCopy)
    {
        protected override int TemporalFractionalDigits => MySqlTypeHooks.TemporalFractionalDigits;

        protected override object ExpectedValue(object written) => MySqlTypeHooks.ExpectedValue(base.ExpectedValue(written));

        //MySQL time holds +-838:59:59, so a TimeSpan longer than a day is stored.
        protected override bool RefusesTimeSpanOutsideADay => false;
    }
}

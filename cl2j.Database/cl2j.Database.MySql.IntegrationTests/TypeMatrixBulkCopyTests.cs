using cl2j.Database.IntegrationTests;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    /// <summary>
    ///     MySQL's run of the shared <see cref="cl2j.Database.IntegrationTests.TypeMatrixTestsBase"/>. The hooks are MySQL's: six fractional digits, a datetime that starts in the year 1000, an instant instead of an offset.
    /// </summary>
    [Collection(MySqlTestGroup.Name)]
    public class TypeMatrixBulkCopyTests(MySqlFixture fixture) : TypeMatrixTestsBase(fixture.BulkCopy)
    {
        protected override int TemporalFractionalDigits => MySqlTypeHooks.TemporalFractionalDigits;

        protected override object ValueToWrite(object value) => MySqlTypeHooks.ValueToWrite(base.ValueToWrite(value));

        protected override object ExpectedValue(object written) => MySqlTypeHooks.ExpectedValue(base.ExpectedValue(written));

        protected override string ExpectedColumnType(Type clrType) => MySqlTypeHooks.ExpectedColumnType(clrType);
    }
}

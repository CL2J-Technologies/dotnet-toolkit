using cl2j.Database.IntegrationTests;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    /// <summary>
    ///     MySQL's run of the shared <see cref="cl2j.Database.IntegrationTests.NullableColumnTestsBase"/>. The catalog names MySQL's types differently.
    /// </summary>
    [Collection(MySqlTestGroup.Name)]
    public class NullableColumnTests(MySqlFixture fixture) : NullableColumnTestsBase(fixture)
    {
        protected override string DateTimeTypeName => "datetime";

        protected override string GuidTypeName => "char";
    }
}

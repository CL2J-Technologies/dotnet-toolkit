using cl2j.Database.IntegrationTests;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    /// <summary>
    ///     MySQL's run of the shared <see cref="cl2j.Database.IntegrationTests.WideValueTestsBase"/>. A double is a double and a Guid is a char(36).
    /// </summary>
    [Collection(MySqlTestGroup.Name)]
    public class WideValueTests(MySqlFixture fixture) : WideValueTestsBase(fixture)
    {
        protected override string DoubleTypeName => "double";

        protected override string GuidTypeName => "char";
    }
}

using cl2j.Database.IntegrationTests;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    /// <summary>
    ///     MySQL's run of the shared <see cref="cl2j.Database.IntegrationTests.TypeKeyAndForeignTableTestsBase"/>, with the batched INSERT bulk strategy.
    /// </summary>
    [Collection(MySqlTestGroup.Name)]
    public class TypeKeyAndForeignTableTests(MySqlFixture fixture) : TypeKeyAndForeignTableTestsBase(fixture)
    {
    }
}

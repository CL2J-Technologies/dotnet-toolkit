using cl2j.Database.IntegrationTests;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    /// <summary>
    ///     MySQL's run of the shared <see cref="cl2j.Database.IntegrationTests.DatabaseFacadeTestsBase"/>, with the batched INSERT bulk strategy.
    /// </summary>
    [Collection(MySqlTestGroup.Name)]
    public class DatabaseFacadeTests(MySqlFixture fixture) : DatabaseFacadeTestsBase(fixture)
    {
    }
}

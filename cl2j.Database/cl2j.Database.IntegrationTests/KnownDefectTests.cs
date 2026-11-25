using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     SQL Server's run of the shared <see cref="KnownDefectTestsBase"/> (the defects that
    ///     reach a server).
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class KnownDefectTests(SqlServerFixture fixture) : KnownDefectTestsBase(fixture)
    {
    }
}

using Xunit;

namespace cl2j.Database.PostgreSql.IntegrationTests
{
    /// <summary>PostgreSQL's run of the shared <see cref="cl2j.Database.IntegrationTests.ReaderTestsBase"/>.</summary>
    [Collection(PostgreSqlGroup.Name)]
    public class ReaderTests(PostgreSqlFixture fixture) : cl2j.Database.IntegrationTests.ReaderTestsBase(fixture)
    {
        //MomentValue keeps its default: the -04:00 offset goes through PrepareParameter as UTC, and
        //DateTimeOffset equality compares the instant.
    }
}

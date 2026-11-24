using cl2j.Database.IntegrationTests;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    /// <summary>
    ///     MySQL's run of the shared <see cref="cl2j.Database.IntegrationTests.ReaderTestsBase"/>. MySQL keeps no offset, so the moment under test is UTC: equality compares instants, but the offset assertions need the value MySQL hands back.
    /// </summary>
    [Collection(MySqlTestGroup.Name)]
    public class ReaderTests(MySqlFixture fixture) : ReaderTestsBase(fixture)
    {
        protected override DateTimeOffset MomentValue => new(2026, 9, 6, 14, 30, 15, TimeSpan.Zero);
    }
}

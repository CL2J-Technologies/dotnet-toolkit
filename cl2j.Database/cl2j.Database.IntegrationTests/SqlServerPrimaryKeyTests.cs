using cl2j.Database.DataAnnotations;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     The string-key constraint used to be written <c>PRIMARY KEY CLUSTERED(... ASC)</c>. It is now
    ///     a plain <c>PRIMARY KEY (...)</c>, because MySQL refuses the first keyword and PostgreSQL the
    ///     second. SQL Server must end up with the same table either way, and only the server can say
    ///     so: what it built is read back from the catalog.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class SqlServerPrimaryKeyTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        public async ValueTask InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(PkClusteredRow), CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(PkClusteredRow), CancellationToken.None);
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task A_string_key_is_still_a_clustered_primary_key_without_saying_so()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<PkClusteredRow>();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT type_desc FROM sys.indexes WHERE object_id = OBJECT_ID(N'PkClustered') AND is_primary_key = 1";
            var indexType = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

            Assert.Equal("CLUSTERED", indexType);
        }
    }

    [Table("PkClustered")]
    public sealed class PkClusteredRow
    {
        [Column(Name = "Code", Key = KeyType.Key, Length = 50)]
        public string Code { get; set; } = string.Empty;

        [Column(Name = "Label", Length = 50)]
        public string Label { get; set; } = string.Empty;
    }
}

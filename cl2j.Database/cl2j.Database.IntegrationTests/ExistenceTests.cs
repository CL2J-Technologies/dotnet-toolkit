using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     TableExists and DropTable used to answer rather than report. TableExists ran
    ///     SELECT TOP 1 * against the table and read any exception as "no"; DropTable had an empty
    ///     catch, so a failed drop looked exactly like a successful one. Both turned an
    ///     infrastructure problem into a plausible but wrong answer.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class ExistenceTests(SqlServerFixture fixture)
    {
        [Fact]
        public async Task A_table_that_was_created_is_reported_as_existing()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(TransactedRow), CancellationToken.None);
            await connection.CreateTable<TransactedRow>();
            try
            {
                Assert.True(await connection.TableExists<TransactedRow>());
            }
            finally
            {
                await connection.DropTableIfExists(typeof(TransactedRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task A_table_that_was_never_created_is_reported_as_missing()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(TransactedRow), CancellationToken.None);

            Assert.False(await connection.TableExists<TransactedRow>());
        }

        [Fact]
        public async Task Dropping_a_table_that_is_not_there_is_an_error_rather_than_a_shrug()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(TransactedRow), CancellationToken.None);

            await Assert.ThrowsAsync<SqlException>(
                () => connection.DropTable(typeof(TransactedRow), CancellationToken.None));
        }

        [Fact]
        public async Task Drop_table_if_exists_is_still_the_forgiving_one()
        {
            //Which is where "only if it is there" belongs, rather than inside DropTable.
            await using var connection = await fixture.OpenAsync();

            await connection.DropTableIfExists(typeof(TransactedRow), CancellationToken.None);
            await connection.DropTableIfExists(typeof(TransactedRow), CancellationToken.None);

            Assert.False(await connection.TableExists<TransactedRow>());
        }
    }
}

using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Every write overload takes an optional <c>DbTransaction</c>, and none of them had ever
    ///     been exercised with one.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class TransactionTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(TransactedRow), CancellationToken.None);
            await connection.CreateTable<TransactedRow>();
        }

        public async Task DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(TransactedRow), CancellationToken.None);
        }

        [Fact]
        public async Task A_committed_insert_is_visible_afterwards()
        {
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<TransactedRow>();

            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await connection.Insert(new TransactedRow { Id = id, Label = "kept" }, CancellationToken.None, transaction);
                await transaction.CommitAsync();
            }

            var read = await connection.QueryKey<TransactedRow>(id);
            Assert.NotNull(read);
            Assert.Equal("kept", read.Label);
        }

        [Fact]
        public async Task A_rolled_back_insert_leaves_nothing_behind()
        {
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<TransactedRow>();

            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await connection.Insert(new TransactedRow { Id = id, Label = "discarded" }, CancellationToken.None, transaction);
                await transaction.RollbackAsync();
            }

            Assert.Null(await connection.QueryKey<TransactedRow>(id));
        }

        [Fact]
        public async Task A_rollback_undoes_every_statement_of_the_transaction()
        {
            await using var connection = await fixture.OpenAsync();
            var first = await connection.NewKey<TransactedRow>();
            var second = await connection.NewKey<TransactedRow>();

            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await connection.Insert(new TransactedRow { Id = first, Label = "a" }, CancellationToken.None, transaction);
                await connection.Insert(new TransactedRow { Id = second, Label = "b" }, CancellationToken.None, transaction);
                await connection.Update(new TransactedRow { Id = first, Label = "changed" }, CancellationToken.None, transaction);
                await transaction.RollbackAsync();
            }

            Assert.Null(await connection.QueryKey<TransactedRow>(first));
            Assert.Null(await connection.QueryKey<TransactedRow>(second));
        }

        [Fact]
        public async Task A_delete_inside_a_rolled_back_transaction_does_not_remove_the_row()
        {
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<TransactedRow>();
            await connection.Insert(new TransactedRow { Id = id, Label = "survivor" });

            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await connection.DeleteKey<TransactedRow>(id, CancellationToken.None, transaction);
                await transaction.RollbackAsync();
            }

            Assert.NotNull(await connection.QueryKey<TransactedRow>(id));
        }
    }
}

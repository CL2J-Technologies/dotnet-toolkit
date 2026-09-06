using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     BulkInsert, and the batching that QueryKeys does above 2000 keys.
    ///
    ///     <para>
    ///     The unit tests prove the key list is split correctly and that no key is lost from the
    ///     split. Only a server proves the results of those separate statements come back as one
    ///     set, with every row present exactly once.
    ///     </para>
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class BulkAndBatchTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(BulkRow), CancellationToken.None);
            await connection.DropTableIfExists(typeof(KeyBatchRow), CancellationToken.None);
            await connection.CreateTable<BulkRow>();
            await connection.CreateTable<KeyBatchRow>();
        }

        public async Task DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(BulkRow), CancellationToken.None);
            await connection.DropTableIfExists(typeof(KeyBatchRow), CancellationToken.None);
        }

        [Fact]
        public async Task Bulk_insert_writes_every_row_with_its_own_values()
        {
            await using var connection = await fixture.OpenAsync();

            var rows = new List<BulkRow>();
            for (var i = 0; i < 500; i++)
                rows.Add(new BulkRow { Id = await connection.NewKey<BulkRow>(), Position = i });

            await connection.BulkInsert(rows);

            var read = await connection.Query<BulkRow>("SELECT [Id],[Position] FROM [Bulk]");

            Assert.Equal(500, read.Count);
            Assert.Equal(Enumerable.Range(0, 500), read.Select(r => r.Position).OrderBy(p => p));
        }

        [Fact]
        public async Task Query_keys_returns_every_row_across_a_batch_boundary()
        {
            //2500 keys, so the split produces two statements: 2000 then 500. The point is the
            //concatenation, which the unit tests cannot reach.
            await using var connection = await fixture.OpenAsync();

            const int count = 2500;
            var rows = new List<KeyBatchRow>();
            for (var i = 0; i < count; i++)
                rows.Add(new KeyBatchRow { Id = $"key-{i:D5}", Position = i });

            await connection.BulkInsert(rows);

            var keys = rows.Select(r => (object)r.Id).ToList();
            var read = await connection.QueryKeys<KeyBatchRow>(keys);

            Assert.Equal(count, read.Count);
            Assert.Equal(count, read.Select(r => r.Id).Distinct().Count());
            Assert.Equal(Enumerable.Range(0, count), read.Select(r => r.Position).OrderBy(p => p));
        }

        [Fact]
        public async Task Query_keys_with_no_keys_returns_nothing_and_touches_no_statement()
        {
            //An empty IN clause is not valid SQL. If a statement were built for an empty list, this
            //would throw rather than return an empty set.
            await using var connection = await fixture.OpenAsync();

            var read = await connection.QueryKeys<KeyBatchRow>([]);

            Assert.Empty(read);
        }

        [Fact]
        public async Task Query_keys_returns_only_the_keys_asked_for()
        {
            await using var connection = await fixture.OpenAsync();

            await connection.BulkInsert(
            [
                new KeyBatchRow { Id = "wanted-1", Position = 1 },
                new KeyBatchRow { Id = "wanted-2", Position = 2 },
                new KeyBatchRow { Id = "ignored", Position = 3 },
            ]);

            var read = await connection.QueryKeys<KeyBatchRow>(["wanted-1", "wanted-2"]);

            Assert.Equal(2, read.Count);
            Assert.DoesNotContain(read, r => r.Id == "ignored");
        }

        [Fact]
        public async Task A_key_containing_a_quote_is_matched_rather_than_breaking_the_statement()
        {
            //The end-to-end version of GHSA-8hpr-28mq-ffc8. Before the fix this produced a syntax
            //error; the value had escaped its literal.
            await using var connection = await fixture.OpenAsync();

            await connection.BulkInsert([new KeyBatchRow { Id = "O'Brien", Position = 99 }]);

            var read = await connection.QueryKeys<KeyBatchRow>(["O'Brien"]);

            var row = Assert.Single(read);
            Assert.Equal(99, row.Position);
        }
    }
}

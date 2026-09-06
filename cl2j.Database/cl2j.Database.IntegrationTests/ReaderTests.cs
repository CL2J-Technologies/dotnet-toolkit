using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Materialisation. This is the path with the widest blast radius and it had no coverage
    ///     at all: every read of every consumer goes through it.
    ///
    ///     <para>
    ///     It is also not a simple mapper. DbReaderExtensions generates C# per type at runtime,
    ///     compiles it with Roslyn, caches the compiled script and runs it against the reader. The
    ///     generated code addresses columns by <b>ordinal</b>, in descriptor order.
    ///     </para>
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class ReaderTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(ReaderShapesRow), CancellationToken.None);
            await connection.CreateTable<ReaderShapesRow>();
        }

        public async Task DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(ReaderShapesRow), CancellationToken.None);
        }

        [Fact]
        public async Task A_row_survives_the_write_read_cycle_intact()
        {
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<ReaderShapesRow>();
            var moment = new DateTimeOffset(2026, 9, 6, 14, 30, 15, TimeSpan.FromHours(-4));

            await connection.Insert(new ReaderShapesRow
            {
                Id = id,
                Text = "présent",
                Count = 17,
                Flag = true,
                Moment = moment,
                Payload = "hello",
            });

            var read = await connection.QueryKey<ReaderShapesRow>(id);

            Assert.NotNull(read);
            Assert.Equal("présent", read.Text);
            Assert.Equal(17, read.Count);
            Assert.True(read.Flag);
            Assert.Equal(moment, read.Moment);
            Assert.Equal("hello", read.Payload);
        }

        [Fact]
        public async Task A_null_text_column_reads_back_as_null()
        {
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<ReaderShapesRow>();

            await connection.Insert(new ReaderShapesRow
            {
                Id = id,
                Text = null,
                Count = 0,
                Payload = "x",
            });

            var read = await connection.QueryKey<ReaderShapesRow>(id);

            Assert.NotNull(read);
            Assert.Null(read.Text);
        }

        [Fact]
        public async Task A_non_ascii_value_survives_the_cycle()
        {
            //The columns this library creates for strings are varchar, not nvarchar. Whatever
            //arrives here is what a consumer actually gets back.
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<ReaderShapesRow>();

            await connection.Insert(new ReaderShapesRow { Id = id, Text = "Renée", Payload = "x" });

            var read = await connection.QueryKey<ReaderShapesRow>(id);

            Assert.Equal("Renée", read!.Text);
        }

        [Fact]
        public async Task An_empty_result_set_reads_as_an_empty_list()
        {
            await using var connection = await fixture.OpenAsync();

            var rows = await connection.Query<ReaderShapesRow>(
                "SELECT [Id],[Text],[Count],[Flag],[Moment],[Payload] FROM [ReaderShapes] WHERE [Count] = @Count",
                new { Count = -999 });

            Assert.Empty(rows);
        }

        [Fact]
        public async Task A_missing_row_reads_as_null_rather_than_throwing()
        {
            await using var connection = await fixture.OpenAsync();

            var missing = await connection.QueryKey<ReaderShapesRow>("no-such-key");

            Assert.Null(missing);
        }

        [Fact]
        public async Task A_reordered_select_still_lands_each_value_in_its_own_property()
        {
            //Before columns were bound by name this threw: the reordering put a plain string where
            //the JSON column was expected and deserialisation failed. That was the lucky case —
            //see the test below for the one that did not fail.
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<ReaderShapesRow>();
            await connection.Insert(new ReaderShapesRow { Id = id, Text = "text-value", Payload = "payload-value" });

            var rows = await connection.Query<ReaderShapesRow>(
                "SELECT [Id],[Payload],[Count],[Flag],[Moment],[Text] FROM [ReaderShapes] WHERE [Id] = @Id",
                new { Id = id });

            var row = Assert.Single(rows);
            Assert.Equal("text-value", row.Text);
            Assert.Equal("payload-value", row.Payload);
        }

        [Fact]
        public async Task A_select_that_omits_a_column_names_the_one_it_is_missing()
        {
            //Reading by name turns a misalignment into a failure that says which column it wanted,
            //instead of an index that means nothing to the caller.
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<ReaderShapesRow>();
            await connection.Insert(new ReaderShapesRow { Id = id, Text = "value", Payload = "x" });

            var exception = await Assert.ThrowsAnyAsync<Exception>(() => connection.Query<ReaderShapesRow>(
                "SELECT [Id],[Count],[Flag],[Moment],[Payload] FROM [ReaderShapes] WHERE [Id] = @Id",
                new { Id = id }));

            Assert.Contains("Text", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Extra_columns_in_the_select_are_ignored()
        {
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<ReaderShapesRow>();
            await connection.Insert(new ReaderShapesRow { Id = id, Text = "kept", Count = 4, Payload = "x" });

            var rows = await connection.Query<ReaderShapesRow>(
                "SELECT *, 1 AS [Unrelated] FROM [ReaderShapes] WHERE [Id] = @Id",
                new { Id = id });

            var row = Assert.Single(rows);
            Assert.Equal("kept", row.Text);
            Assert.Equal(4, row.Count);
        }

        [Fact]
        public async Task Columns_are_bound_by_name_not_by_position()
        {
            //The case this was all about. The reader used to address columns by ordinal in
            //descriptor order, so a reordering that swapped two columns of the same shape gave no
            //sign at all: no exception, no warning, just an object whose fields held each other's
            //values.
            //
            //It mattered because raw SQL is a supported entry point. A consumer writing SELECT *
            //relied on the physical column order of the table matching the property order of the
            //type — which nothing enforces, and which an ALTER TABLE can change. See issue #25.
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(TwoTextsRow), CancellationToken.None);
            await connection.CreateTable<TwoTextsRow>();
            try
            {
                var id = await connection.NewKey<TwoTextsRow>();
                await connection.Insert(new TwoTextsRow { Id = id, First = "one", Second = "two" });

                var rows = await connection.Query<TwoTextsRow>(
                    "SELECT [Id],[Second],[First] FROM [TwoTexts] WHERE [Id] = @Id",
                    new { Id = id });

                var row = Assert.Single(rows);

                Assert.Equal("one", row.First);
                Assert.Equal("two", row.Second);
            }
            finally
            {
                await connection.DropTableIfExists(typeof(TwoTextsRow), CancellationToken.None);
            }
        }
    }
}

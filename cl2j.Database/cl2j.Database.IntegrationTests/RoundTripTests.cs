using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     The write-then-read cycle for every operation that has one. Nothing here is asserted on
    ///     the SQL: what matters is that the row that comes back is the row that went in.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class RoundTripTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();

            //Creating the table through the library is itself a test: it is the first time the
            //generated DDL is handed to a server rather than compared to a string.
            await connection.DropTableIfExists(typeof(RoundTripRow), CancellationToken.None);
            await connection.CreateTable<RoundTripRow>();
        }

        public async Task DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(RoundTripRow), CancellationToken.None);
        }

        [Fact]
        public async Task The_generated_create_table_is_valid_sql()
        {
            await using var connection = await fixture.OpenAsync();

            //InitializeAsync already ran it. This asserts the server agrees the table is there.
            Assert.True(await connection.TableExists<RoundTripRow>());
        }

        [Fact]
        public async Task An_inserted_row_comes_back_by_its_key()
        {
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<RoundTripRow>();

            await connection.Insert(new RoundTripRow { Id = id, Name = "first", Quantity = 3 });

            var read = await connection.QueryKey<RoundTripRow>(id);

            Assert.NotNull(read);
            Assert.Equal(id, read.Id);
            Assert.Equal("first", read.Name);
            Assert.Equal(3, read.Quantity);
        }

        [Fact]
        public async Task QueryKey_returns_the_row_asked_for_and_not_the_first_one()
        {
            //The regression test for the defect where GetQueryByKeyStatement emitted no WHERE
            //clause: every read returned the first row of the table. With one row in the table
            //that is indistinguishable from correct, so there are three.
            await using var connection = await fixture.OpenAsync();

            var ids = new List<string>();
            foreach (var name in new[] { "alpha", "beta", "gamma" })
            {
                var id = await connection.NewKey<RoundTripRow>();
                await connection.Insert(new RoundTripRow { Id = id, Name = name, Quantity = 1 });
                ids.Add(id);
            }

            var second = await connection.QueryKey<RoundTripRow>(ids[1]);

            Assert.NotNull(second);
            Assert.Equal("beta", second.Name);
        }

        [Fact]
        public async Task An_updated_row_keeps_its_key_and_changes_its_columns()
        {
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<RoundTripRow>();
            await connection.Insert(new RoundTripRow { Id = id, Name = "before", Quantity = 1 });

            await connection.Update(new RoundTripRow { Id = id, Name = "after", Quantity = 9 });

            var read = await connection.QueryKey<RoundTripRow>(id);
            Assert.Equal("after", read!.Name);
            Assert.Equal(9, read.Quantity);
        }

        [Fact]
        public async Task Update_column_changes_only_the_named_column()
        {
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<RoundTripRow>();
            await connection.Insert(new RoundTripRow { Id = id, Name = "kept", Quantity = 5 });

            await connection.UpdateColumn(new RoundTripRow { Id = id }, "Quantity", 42);

            var read = await connection.QueryKey<RoundTripRow>(id);
            Assert.Equal(42, read!.Quantity);
            Assert.Equal("kept", read.Name);
        }

        [Fact]
        public async Task A_deleted_row_is_gone()
        {
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<RoundTripRow>();
            await connection.Insert(new RoundTripRow { Id = id, Name = "doomed", Quantity = 1 });

            await connection.Delete(new RoundTripRow { Id = id });

            Assert.Null(await connection.QueryKey<RoundTripRow>(id));
        }

        [Fact]
        public async Task Delete_key_removes_only_that_row()
        {
            await using var connection = await fixture.OpenAsync();
            var keep = await connection.NewKey<RoundTripRow>();
            var drop = await connection.NewKey<RoundTripRow>();
            await connection.Insert(new RoundTripRow { Id = keep, Name = "keep", Quantity = 1 });
            await connection.Insert(new RoundTripRow { Id = drop, Name = "drop", Quantity = 1 });

            await connection.DeleteKey<RoundTripRow>(drop);

            Assert.Null(await connection.QueryKey<RoundTripRow>(drop));
            Assert.NotNull(await connection.QueryKey<RoundTripRow>(keep));
        }

        [Fact]
        public async Task Query_with_raw_sql_and_parameters_returns_matching_rows()
        {
            //The path TourDuChapeau uses: hand-written SQL with an anonymous parameter object.
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<RoundTripRow>();
            await connection.Insert(new RoundTripRow { Id = id, Name = "findable", Quantity = 7 });

            var rows = await connection.Query<RoundTripRow>(
                "SELECT [Id],[Name],[Quantity] FROM [RoundTrip] WHERE [Name] = @Name",
                new { Name = "findable" });

            var row = Assert.Single(rows);
            Assert.Equal(7, row.Quantity);
        }

        [Fact]
        public async Task Query_single_with_raw_sql_returns_one_row_or_null()
        {
            await using var connection = await fixture.OpenAsync();

            var missing = await connection.QuerySingle<RoundTripRow>(
                "SELECT [Id],[Name],[Quantity] FROM [RoundTrip] WHERE [Name] = @Name",
                new { Name = "no such name" });

            Assert.Null(missing);
        }

        [Fact]
        public async Task Drop_table_if_exists_is_safe_to_call_twice()
        {
            await using var connection = await fixture.OpenAsync();

            await connection.DropTableIfExists(typeof(RoundTripRow), CancellationToken.None);
            await connection.DropTableIfExists(typeof(RoundTripRow), CancellationToken.None);

            Assert.False(await connection.TableExists<RoundTripRow>());

            //Put it back for whatever runs next in this class.
            await connection.CreateTable<RoundTripRow>();
        }
    }
}

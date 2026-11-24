using cl2j.Database.Exceptions;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     The overloads and branches of the <c>DbConnection</c> extensions that the other classes
    ///     here do not reach: the generic shorthands, the conditional creation, the query-by-example
    ///     forms, and the key checks.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class ConnectionGapTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(FacadeRow), CancellationToken.None);
            await connection.DropTableIfExists(typeof(PlainKeyedRow), CancellationToken.None);
            await connection.CreateTable<FacadeRow>();
        }

        public async Task DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(FacadeRow), CancellationToken.None);
            await connection.DropTableIfExists(typeof(PlainKeyedRow), CancellationToken.None);
        }

        [Fact]
        public async Task CreateTableIfRequired_creates_a_missing_table_and_leaves_an_existing_one_alone()
        {
            await using var connection = await fixture.OpenAsync();
            Assert.False(await connection.TableExists<PlainKeyedRow>());

            await connection.CreateTableIfRequired<PlainKeyedRow>();
            Assert.True(await connection.TableExists<PlainKeyedRow>());

            await connection.Insert(new PlainKeyedRow { Code = "kept", Label = "x" });
            await connection.CreateTableIfRequired<PlainKeyedRow>();

            Assert.NotNull(await connection.QueryKey<PlainKeyedRow>("kept"));
        }

        [Fact]
        public async Task The_generic_DropTable_removes_the_table()
        {
            await using var connection = await fixture.OpenAsync();

            await connection.DropTable<FacadeRow>();

            Assert.False(await connection.TableExists<FacadeRow>());
        }

        [Fact]
        public async Task The_generic_DropTableIfExists_is_quiet_about_a_table_that_is_not_there()
        {
            await using var connection = await fixture.OpenAsync();

            await connection.DropTableIfExists<FacadeRow>();
            await connection.DropTableIfExists<FacadeRow>();

            Assert.False(await connection.TableExists<FacadeRow>());
        }

        [Fact]
        public async Task Insert_of_a_string_key_has_no_generated_value_to_hand_back()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<PlainKeyedRow>();

            var generated = await connection.Insert<PlainKeyedRow, string>(new PlainKeyedRow { Code = "mine", Label = "x" });

            Assert.Null(generated);
            Assert.NotNull(await connection.QueryKey<PlainKeyedRow>("mine"));
        }

        [Fact]
        public async Task BulkInsert_fills_in_the_auto_generated_keys_before_writing()
        {
            await using var connection = await fixture.OpenAsync();
            var rows = Enumerable.Range(0, 20)
                .Select(i => new FacadeRow { Name = $"n{i}", Quantity = i, Moment = DateTimeOffset.UtcNow })
                .ToList();

            await connection.BulkInsert(rows);

            Assert.All(rows, r => Assert.StartsWith("fac_", r.Id, StringComparison.Ordinal));
            Assert.Equal(20, rows.Select(r => r.Id).Distinct().Count());

            var read = await connection.QueryKeys<FacadeRow>(rows.Select(r => (object)r.Id));
            Assert.Equal(20, read.Count);
        }

        [Fact]
        public async Task Execute_binds_the_properties_of_its_argument()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.Insert(new FacadeRow { Name = "a", Quantity = 1, Moment = DateTimeOffset.UtcNow });

            var touched = await connection.Execute("UPDATE [Facade] SET [Quantity] = @Quantity WHERE [Name] = @Name", new { Quantity = 5, Name = "a" });

            Assert.Equal(1, touched);
            var read = await connection.Query<FacadeRow>(new { Name = "a" });
            Assert.Equal(5, Assert.Single(read).Quantity);
        }

        [Fact]
        public async Task BuildSelectStatement_lists_the_columns_and_appends_a_suffix()
        {
            await using var connection = await fixture.OpenAsync();

            var plain = connection.BuildSelectStatement<FacadeRow>();
            var suffixed = connection.BuildSelectStatement<FacadeRow>("WHERE [Quantity] = 1");

            Assert.Equal("SELECT [Id],[Name],[Quantity],[Moment] FROM [Facade]", plain);
            Assert.Equal(plain + " WHERE [Quantity] = 1", suffixed);
        }

        [Fact]
        public async Task Query_by_example_filters_and_a_null_example_does_not()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.Insert(new FacadeRow { Name = "a", Quantity = 1, Moment = DateTimeOffset.UtcNow });
            await connection.Insert(new FacadeRow { Name = "b", Quantity = 2, Moment = DateTimeOffset.UtcNow });

            var filtered = await connection.Query<FacadeRow>(new { Name = "b" });
            var everything = await connection.Query<FacadeRow>((object?)null);

            Assert.Equal("b", Assert.Single(filtered).Name);
            Assert.Equal(2, everything.Count);
        }

        [Fact]
        public async Task QuerySingle_by_example_finds_the_row_or_reports_none()
        {
            await using var connection = await fixture.OpenAsync();
            var row = new FacadeRow { Name = "a", Quantity = 1, Moment = DateTimeOffset.UtcNow };
            await connection.Insert(row);

            var found = await connection.QuerySingle<FacadeRow>((object)new { row.Id });
            var missing = await connection.QuerySingle<FacadeRow>((object)new { Id = "fac_missing" });

            Assert.Equal("a", found!.Name);
            Assert.Null(missing);
        }

        [Fact]
        public async Task QueryKey_on_a_type_with_two_keys_is_refused_by_name()
        {
            //There is no table behind TwoKeysRow: the check runs before anything is sent.
            await using var connection = await fixture.OpenAsync();

            var ex = await Assert.ThrowsAsync<DatabaseException>(() => connection.QueryKey<TwoKeysRow>("a"));

            Assert.Contains("exactly one key", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task UpdateColumn_with_no_value_for_a_timestamp_stamps_the_current_time()
        {
            //A null for a DateTimeOffset column with no declared default is replaced by "now"
            //rather than written as NULL (the column is NOT NULL). Characterised, not endorsed.
            await using var connection = await fixture.OpenAsync();
            var row = new FacadeRow { Name = "a", Quantity = 1, Moment = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero) };
            await connection.Insert(row);
            var before = DateTimeOffset.UtcNow.AddMinutes(-1);

            await connection.UpdateColumn(row, "Moment", (DateTimeOffset?)null);

            var read = await connection.QueryKey<FacadeRow>(row.Id);
            Assert.True(read!.Moment > before, $"Moment was {read.Moment:O}");
        }

        [Fact]
        public async Task UpdateColumn_on_a_column_that_does_not_exist_is_refused()
        {
            await using var connection = await fixture.OpenAsync();
            var row = new FacadeRow { Name = "a", Quantity = 1, Moment = DateTimeOffset.UtcNow };
            await connection.Insert(row);

            await Assert.ThrowsAnyAsync<Exception>(() => connection.UpdateColumn(row, "NoSuchColumn", 1));
        }

        [Fact]
        public async Task Delete_by_entity_removes_the_row_with_that_key()
        {
            await using var connection = await fixture.OpenAsync();
            var doomed = new FacadeRow { Name = "doomed", Quantity = 1, Moment = DateTimeOffset.UtcNow };
            var survivor = new FacadeRow { Name = "survivor", Quantity = 2, Moment = DateTimeOffset.UtcNow };
            await connection.Insert(doomed);
            await connection.Insert(survivor);

            await connection.Delete(doomed);

            var left = Assert.Single(await connection.Query<FacadeRow>());
            Assert.Equal(survivor.Id, left.Id);
        }
    }
}

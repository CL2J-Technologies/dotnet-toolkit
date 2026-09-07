using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Nullable value types. Every one of them used to be created as varchar(MAX), because
    ///     Nullable&lt;int&gt; is not int and missed every branch of the type mapping. A nullable
    ///     column is entirely ordinary, so this was the widest of the three mapping defects.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class NullableColumnTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(NullableRow), CancellationToken.None);
            await connection.CreateTable<NullableRow>();
        }

        public async Task DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(NullableRow), CancellationToken.None);
        }

        [Fact]
        public async Task A_value_survives_the_round_trip()
        {
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<NullableRow>();
            var when = new DateTime(2026, 9, 6, 12, 0, 0, DateTimeKind.Unspecified);
            var reference = Guid.NewGuid();

            await connection.Insert(new NullableRow { Id = id, MaybeCount = 42, MaybeWhen = when, MaybeRef = reference });

            var read = await connection.QueryKey<NullableRow>(id);

            Assert.Equal(42, read!.MaybeCount);
            Assert.Equal(when, read.MaybeWhen);
            Assert.Equal(reference, read.MaybeRef);
        }

        [Fact]
        public async Task An_absent_value_comes_back_null()
        {
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<NullableRow>();

            await connection.Insert(new NullableRow { Id = id });

            var read = await connection.QueryKey<NullableRow>(id);

            Assert.Null(read!.MaybeCount);
            Assert.Null(read.MaybeWhen);
            Assert.Null(read.MaybeRef);
        }

        [Fact]
        public async Task The_columns_are_declared_with_the_types_they_wrap()
        {
            await using var connection = await fixture.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'Nullable'
                """;

            var types = new Dictionary<string, string>(StringComparer.Ordinal);
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    types[reader.GetString(0)] = reader.GetString(1);
            }

            Assert.Equal("int", types["MaybeCount"]);
            Assert.Equal("datetime2", types["MaybeWhen"]);
            Assert.Equal("uniqueidentifier", types["MaybeRef"]);
        }
    }
}

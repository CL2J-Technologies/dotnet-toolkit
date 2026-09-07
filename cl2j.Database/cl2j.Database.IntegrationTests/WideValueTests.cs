using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Three column declarations could not hold the values of the types they were generated
    ///     for. The unit tests assert the declaration; these assert that the value survives, which
    ///     is the part that matters to a caller. See issue #27.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class WideValueTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(WideValuesRow), CancellationToken.None);
            await connection.CreateTable<WideValuesRow>();
        }

        public async Task DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(WideValuesRow), CancellationToken.None);
        }

        [Fact]
        public async Task A_long_past_int_max_survives_the_round_trip()
        {
            //Declared int before, so this value could not be written at all.
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<WideValuesRow>();

            await connection.Insert(new WideValuesRow { Id = id, Big = long.MaxValue, Ref = Guid.NewGuid() });

            var read = await connection.QueryKey<WideValuesRow>(id);

            Assert.Equal(long.MaxValue, read!.Big);
        }

        [Fact]
        public async Task A_fractional_double_keeps_its_fraction()
        {
            //Declared bare decimal before, which SQL Server reads as decimal(18,0): 1.5 came back
            //as 2, rounded on the way in, with nothing to say so.
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<WideValuesRow>();

            await connection.Insert(new WideValuesRow { Id = id, Ratio = 1.5, Ref = Guid.NewGuid() });

            var read = await connection.QueryKey<WideValuesRow>(id);

            Assert.Equal(1.5, read!.Ratio);
        }

        [Fact]
        public async Task A_guid_survives_the_round_trip_as_a_guid()
        {
            //Declared varchar(MAX) before — 36 bytes of text rather than 16 of value.
            await using var connection = await fixture.OpenAsync();
            var id = await connection.NewKey<WideValuesRow>();
            var reference = Guid.NewGuid();

            await connection.Insert(new WideValuesRow { Id = id, Ref = reference });

            var read = await connection.QueryKey<WideValuesRow>(id);

            Assert.Equal(reference, read!.Ref);
        }

        [Fact]
        public async Task The_columns_are_declared_with_the_types_that_hold_them()
        {
            await using var connection = await fixture.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'WideValues'
                ORDER BY COLUMN_NAME
                """;

            var types = new Dictionary<string, string>(StringComparer.Ordinal);
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    types[reader.GetString(0)] = reader.GetString(1);
            }

            Assert.Equal("bigint", types["Big"]);
            Assert.Equal("float", types["Ratio"]);
            Assert.Equal("uniqueidentifier", types["Ref"]);
        }
    }
}

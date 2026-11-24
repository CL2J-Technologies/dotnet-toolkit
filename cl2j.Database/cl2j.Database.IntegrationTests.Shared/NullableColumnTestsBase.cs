using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Nullable value types. Every one of them used to be created as varchar(MAX), because
    ///     Nullable&lt;int&gt; is not int and missed every branch of the type mapping. A nullable
    ///     column is entirely ordinary, so this was the widest of the three mapping defects.
    /// </summary>
    public abstract class NullableColumnTestsBase(IDatabaseFixture fixture) : IAsyncLifetime
    {
        //The DATA_TYPE that INFORMATION_SCHEMA.COLUMNS reports for the column each CLR type maps
        //to. The names are the catalog's, which differ per provider; override what yours says.

        /// <summary>The catalog type of an <c>int?</c> column. SQL Server: <c>int</c>.</summary>
        protected virtual string IntTypeName => "int";

        /// <summary>The catalog type of a <c>DateTime?</c> column. SQL Server: <c>datetime2</c>.</summary>
        protected virtual string DateTimeTypeName => "datetime2";

        /// <summary>The catalog type of a <c>Guid?</c> column. SQL Server: <c>uniqueidentifier</c>.</summary>
        protected virtual string GuidTypeName => "uniqueidentifier";

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
            command.CommandText = $"""
                SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'Nullable'{fixture.CatalogScopeFilter}
                """;

            var types = new Dictionary<string, string>(StringComparer.Ordinal);
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    types[reader.GetString(0)] = reader.GetString(1);
            }

            Assert.Equal(IntTypeName, types["MaybeCount"]);
            Assert.Equal(DateTimeTypeName, types["MaybeWhen"]);
            Assert.Equal(GuidTypeName, types["MaybeRef"]);
        }
    }
}

using cl2j.Database.DataAnnotations;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Four defects found while prototyping a MySQL provider, each checked against a real server:
    ///     a bulk insert dropped string <c>KeyType.Key</c> columns, a <c>long</c> key could not be
    ///     created, a <c>Guid</c> key's DDL repeated its type, and a NULL JSON column could not be
    ///     read back.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class KeyAndJsonFixTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        private static readonly Type[] Tables = [typeof(StringKeyBulkRow), typeof(LongKeyRow), typeof(GuidKeyRow), typeof(JsonNullRow)];

        public async Task InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            foreach (var table in Tables)
                await connection.DropTableIfExists(table, CancellationToken.None);
        }

        public async Task DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            foreach (var table in Tables)
                await connection.DropTableIfExists(table, CancellationToken.None);
        }

        [Fact]
        public async Task BulkInsert_sends_a_string_key()
        {
            //The key was left out of the copy, and the NOT NULL primary key refused every row.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<StringKeyBulkRow>();

            await connection.BulkInsert(new[]
            {
                new StringKeyBulkRow { Code = "a", Label = "first" },
                new StringKeyBulkRow { Code = "b", Label = "second" },
            });

            Assert.Equal("second", (await connection.QueryKey<StringKeyBulkRow>("b"))!.Label);
            Assert.Equal(2, (await connection.Query<StringKeyBulkRow>()).Count);
        }

        [Fact]
        public async Task A_long_key_table_is_created_and_hands_back_its_identity()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<LongKeyRow>();

            var first = await connection.Insert<LongKeyRow, long>(new LongKeyRow { Label = "one" });
            var second = await connection.Insert<LongKeyRow, long>(new LongKeyRow { Label = "two" });

            Assert.Equal(first + 1, second);
            Assert.Equal("two", (await connection.QueryKey<LongKeyRow>(second))!.Label);
        }

        [Fact]
        public async Task A_long_key_is_left_to_the_server_by_BulkInsert()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<LongKeyRow>();

            await connection.BulkInsert(new[] { new LongKeyRow { Label = "x" }, new LongKeyRow { Label = "y" } });

            var rows = await connection.Query<LongKeyRow>();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.True(r.Id > 0));
        }

        [Fact]
        public async Task A_guid_key_table_is_created_and_the_server_fills_the_key()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<GuidKeyRow>();

            await connection.Insert(new GuidKeyRow { Label = "single" });
            await connection.BulkInsert(new[] { new GuidKeyRow { Label = "bulk1" }, new GuidKeyRow { Label = "bulk2" } });

            var rows = await connection.Query<GuidKeyRow>();
            Assert.Equal(3, rows.Count);
            Assert.All(rows, r => Assert.NotEqual(Guid.Empty, r.Id));
            Assert.Equal(3, rows.Select(r => r.Id).Distinct().Count());
        }

        [Fact]
        public async Task A_null_json_property_survives_a_bulk_insert()
        {
            //BulkInsert writes SQL NULL for it; the reader called GetString on the NULL and threw.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<JsonNullRow>();

            await connection.BulkInsert(new[]
            {
                new JsonNullRow { Code = "null", Tags = null },
                new JsonNullRow { Code = "set", Tags = ["a", "b"] },
            });

            Assert.Null((await connection.QueryKey<JsonNullRow>("null"))!.Tags);
            Assert.Equal(["a", "b"], (await connection.QueryKey<JsonNullRow>("set"))!.Tags);
        }

        [Fact]
        public async Task A_null_json_property_survives_an_insert()
        {
            //Insert writes the text 'null' rather than SQL NULL: both must read back as null.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<JsonNullRow>();

            await connection.Insert(new JsonNullRow { Code = "null", Tags = null });

            Assert.Null((await connection.QueryKey<JsonNullRow>("null"))!.Tags);
        }
    }

    [Table("FixStringKeyBulk")]
    public sealed class StringKeyBulkRow
    {
        [Column(Name = "Code", Key = KeyType.Key, Length = 20)]
        public string Code { get; set; } = string.Empty;

        [Column(Name = "Label", Length = 50)]
        public string Label { get; set; } = string.Empty;
    }

    [Table("FixLongKey")]
    public sealed class LongKeyRow
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public long Id { get; set; }

        [Column(Name = "Label", Length = 50)]
        public string Label { get; set; } = string.Empty;
    }

    [Table("FixGuidKey")]
    public sealed class GuidKeyRow
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public Guid Id { get; set; }

        [Column(Name = "Label", Length = 50)]
        public string Label { get; set; } = string.Empty;
    }

    [Table("FixJsonNull")]
    public sealed class JsonNullRow
    {
        [Column(Name = "Code", Key = KeyType.Key, Length = 20)]
        public string Code { get; set; } = string.Empty;

        [Column(Name = "Tags", Json = true)]
        public List<string>? Tags { get; set; }
    }
}

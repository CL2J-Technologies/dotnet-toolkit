using cl2j.Database.DataAnnotations;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Defects found while checking the 6.6.0 documentation (issues #80 and #81), each checked
    ///     against a real server on every provider.
    /// </summary>
    public abstract class DefectFixTestsBase(IDatabaseFixture fixture) : IAsyncLifetime
    {
        private static readonly Type[] Tables = [typeof(GeneratedKeyBulkRow), typeof(JsonDefaultRow), typeof(JsonUnicodeRow)];

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
        public async Task BulkInsert_of_a_lazy_sequence_keeps_the_generated_keys()
        {
            //The keys were generated on one enumeration of a lazy Select and the provider enumerated
            //again, building fresh items that had no key: the insert failed on a NULL key.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<GeneratedKeyBulkRow>();

            var lazy = Enumerable.Range(1, 5).Select(i => new GeneratedKeyBulkRow { Label = $"row {i}" });
            await connection.BulkInsert(lazy);

            var rows = await connection.Query<GeneratedKeyBulkRow>();
            Assert.Equal(5, rows.Count);
            Assert.All(rows, r => Assert.StartsWith("gkb_", r.Id));
            Assert.Equal(5, rows.Select(r => r.Id).Distinct().Count());
        }

        [Fact]
        public async Task Insert_keeps_a_json_property_that_holds_its_type_default()
        {
            //Count = 0 was not written (the type default), and read back as the initializer's 3.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<JsonDefaultRow>();

            await connection.Insert(new JsonDefaultRow { Code = "a", Retry = new RetryPolicy { Count = 0, Enabled = false, Label = null } });

            var retry = (await connection.QueryKey<JsonDefaultRow>("a"))!.Retry!;
            Assert.Equal(0, retry.Count);
            Assert.False(retry.Enabled);
            Assert.Null(retry.Label);
        }

        [Fact]
        public async Task Update_keeps_a_json_property_that_holds_its_type_default()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<JsonDefaultRow>();
            var row = new JsonDefaultRow { Code = "a", Retry = new RetryPolicy { Count = 7, Enabled = true, Label = "x" } };
            await connection.Insert(row);

            row.Retry = new RetryPolicy { Count = 0, Enabled = false, Label = null };
            await connection.Update(row);

            var retry = (await connection.QueryKey<JsonDefaultRow>("a"))!.Retry!;
            Assert.Equal(0, retry.Count);
            Assert.False(retry.Enabled);
            Assert.Null(retry.Label);
        }

        [Fact]
        public async Task BulkInsert_keeps_a_json_property_that_holds_its_type_default()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<JsonDefaultRow>();

            await connection.BulkInsert(new[]
            {
                new JsonDefaultRow { Code = "a", Retry = new RetryPolicy { Count = 0, Enabled = false, Label = null } },
                new JsonDefaultRow { Code = "b", Retry = new RetryPolicy { Count = 5, Enabled = true } },
            });

            var a = (await connection.QueryKey<JsonDefaultRow>("a"))!.Retry!;
            Assert.Equal(0, a.Count);
            Assert.False(a.Enabled);
            Assert.Null(a.Label);
            Assert.Equal(5, (await connection.QueryKey<JsonDefaultRow>("b"))!.Retry!.Count);
        }

        private static readonly string[] NonLatin = ["Привет", "日本", "café", "😀"];

        [Fact]
        public async Task Insert_round_trips_non_latin_text_in_a_json_column()
        {
            //SQL Server created the column as varchar, so every character outside the collation came
            //back as '?'.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<JsonUnicodeRow>();

            await connection.Insert(new JsonUnicodeRow { Code = "a", Tags = [.. NonLatin] });

            Assert.Equal(NonLatin, (await connection.QueryKey<JsonUnicodeRow>("a"))!.Tags);
        }

        [Fact]
        public async Task BulkInsert_round_trips_non_latin_text_in_a_json_column()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<JsonUnicodeRow>();

            await connection.BulkInsert(new[]
            {
                new JsonUnicodeRow { Code = "a", Tags = [.. NonLatin] },
                new JsonUnicodeRow { Code = "b", Tags = ["plain"] },
            });

            Assert.Equal(NonLatin, (await connection.QueryKey<JsonUnicodeRow>("a"))!.Tags);
            Assert.Equal(["plain"], (await connection.QueryKey<JsonUnicodeRow>("b"))!.Tags);
        }

        [Fact]
        public async Task A_json_column_with_a_length_round_trips_non_latin_text()
        {
            //A bounded column is nvarchar(n) on SQL Server, varchar(n) elsewhere.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<JsonUnicodeRow>();

            await connection.Insert(new JsonUnicodeRow { Code = "a", Bounded = [.. NonLatin] });

            Assert.Equal(NonLatin, (await connection.QueryKey<JsonUnicodeRow>("a"))!.Bounded);
        }
    }

    /// <summary>Every initializer differs from the type default, so a value that is not written reads back wrong.</summary>
    public sealed class RetryPolicy
    {
        public int Count { get; set; } = 3;
        public bool Enabled { get; set; } = true;
        public string? Label { get; set; } = "default";
    }

    [Table("Def660JsonDefault")]
    public sealed class JsonDefaultRow
    {
        [Column(Name = "Code", Key = KeyType.Key, Length = 20)]
        public string Code { get; set; } = string.Empty;

        [Column(Name = "Retry", Json = true)]
        public RetryPolicy? Retry { get; set; }
    }

    [Table("Def660GeneratedKeyBulk")]
    public sealed class GeneratedKeyBulkRow
    {
        [Column(Name = "Id", Key = KeyType.AutoGeneratedKey, Length = 40, KeyPrefix = "gkb_")]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Label", Length = 50)]
        public string Label { get; set; } = string.Empty;
    }

    [Table("Def660JsonUnicode")]
    public sealed class JsonUnicodeRow
    {
        [Column(Name = "Code", Key = KeyType.Key, Length = 20)]
        public string Code { get; set; } = string.Empty;

        [Column(Name = "Tags", Json = true)]
        public List<string>? Tags { get; set; }

        [Column(Name = "Bounded", Json = true, Length = 200)]
        public List<string>? Bounded { get; set; }
    }
}

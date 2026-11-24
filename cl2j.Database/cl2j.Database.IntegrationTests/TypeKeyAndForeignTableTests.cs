using cl2j.Database.DataAnnotations;
using cl2j.Database.TypeMatrix;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Keys of each type that makes sense as one, read by key and by example; and tables the
    ///     library did not create, read through raw SQL. The second half only reports what the
    ///     reader does with column types it never declared: the mapping is by exact type and is not
    ///     extended here.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class TypeKeyAndForeignTableTests(SqlServerFixture fixture)
    {
        [Fact]
        public async Task An_int_key_is_read_by_key_by_keys_and_by_example()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(IdentityKeyedRow), CancellationToken.None);
            await connection.CreateTable<IdentityKeyedRow>();
            try
            {
                var first = await connection.Insert<IdentityKeyedRow, int>(new IdentityKeyedRow { Label = "one" });
                var second = await connection.Insert<IdentityKeyedRow, int>(new IdentityKeyedRow { Label = "two" });

                Assert.Equal("two", (await connection.QueryKey<IdentityKeyedRow>(second))!.Label);
                Assert.Equal("one", Assert.Single(await connection.Query<IdentityKeyedRow>(new { Id = first })).Label);
                Assert.Equal(2, (await connection.QueryKeys<IdentityKeyedRow>([first, second])).Count);
                Assert.Null(await connection.QueryKey<IdentityKeyedRow>(second + 1));
            }
            finally
            {
                await connection.DropTableIfExists(typeof(IdentityKeyedRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task A_long_key_is_read_by_key_by_keys_and_by_example()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(LongKeyRow), CancellationToken.None);
            await connection.CreateTable<LongKeyRow>();
            try
            {
                var first = await connection.Insert<LongKeyRow, long>(new LongKeyRow { Label = "one" });
                var second = await connection.Insert<LongKeyRow, long>(new LongKeyRow { Label = "two" });

                Assert.Equal("two", (await connection.QueryKey<LongKeyRow>(second))!.Label);
                Assert.Equal("one", Assert.Single(await connection.Query<LongKeyRow>(new { Id = first })).Label);
                Assert.Equal(2, (await connection.QueryKeys<LongKeyRow>([first, second])).Count);
            }
            finally
            {
                await connection.DropTableIfExists(typeof(LongKeyRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task A_Guid_key_is_read_by_key_by_keys_and_by_example()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(GuidKeyRow), CancellationToken.None);
            await connection.CreateTable<GuidKeyRow>();
            try
            {
                await connection.Insert(new GuidKeyRow { Label = "one" });
                await connection.Insert(new GuidKeyRow { Label = "two" });
                var rows = await connection.Query<GuidKeyRow>();
                var two = rows.Single(r => r.Label == "two").Id;
                var one = rows.Single(r => r.Label == "one").Id;

                Assert.Equal("two", (await connection.QueryKey<GuidKeyRow>(two))!.Label);
                Assert.Equal("one", Assert.Single(await connection.Query<GuidKeyRow>(new { Id = one })).Label);
                Assert.Equal(2, (await connection.QueryKeys<GuidKeyRow>([one, two])).Count);
                Assert.Null(await connection.QueryKey<GuidKeyRow>(Guid.NewGuid()));
            }
            finally
            {
                await connection.DropTableIfExists(typeof(GuidKeyRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task A_string_key_is_read_by_key_by_keys_and_by_example()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(PlainKeyedRow), CancellationToken.None);
            await connection.CreateTable<PlainKeyedRow>();
            try
            {
                await connection.Insert(new PlainKeyedRow { Code = "a-1", Label = "one" });
                await connection.BulkInsert(new[] { new PlainKeyedRow { Code = "b-2", Label = "two" } });

                Assert.Equal("one", (await connection.QueryKey<PlainKeyedRow>("a-1"))!.Label);
                Assert.Equal("two", Assert.Single(await connection.Query<PlainKeyedRow>(new { Code = "b-2" })).Label);
                Assert.Equal(2, (await connection.QueryKeys<PlainKeyedRow>(["a-1", "b-2"])).Count);
                Assert.Null(await connection.QueryKey<PlainKeyedRow>("c-3"));
            }
            finally
            {
                await connection.DropTableIfExists(typeof(PlainKeyedRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task A_Guid_key_the_caller_supplies_is_not_what_the_server_stores()
        {
            //Pinned: a Guid key is left to the server (DEFAULT NEWID()), for Insert and BulkInsert
            //alike, so a Guid set on the property is ignored. Nothing fails, which is the reason to
            //have the test.
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(GuidKeyRow), CancellationToken.None);
            await connection.CreateTable<GuidKeyRow>();
            try
            {
                var supplied = Guid.NewGuid();

                await connection.Insert(new GuidKeyRow { Id = supplied, Label = "x" });

                Assert.NotEqual(supplied, Assert.Single(await connection.Query<GuidKeyRow>()).Id);
            }
            finally
            {
                await connection.DropTableIfExists(typeof(GuidKeyRow), CancellationToken.None);
            }
        }

        //Raw SQL over columns of types the library did not create. CAST gives the server type
        //without a table.
        private async Task<List<T>> Raw<T>(string select)
        {
            await using var connection = await fixture.OpenAsync();
            return await connection.Query<T>(select);
        }

        [Fact]
        public async Task An_nvarchar_reads_into_a_string_with_its_unicode()
        {
            var rows = await Raw<ForeignString>("SELECT CAST(N'café Привет' AS nvarchar(50)) AS V, CAST(N'П' AS nvarchar(5)) AS R");

            Assert.Equal("café Привет", Assert.Single(rows).V);
            Assert.Equal("П", rows[0].R);
        }

        [Theory]
        [InlineData("datetime")]
        [InlineData("smalldatetime")]
        [InlineData("datetime2(3)")]
        public async Task A_datetime_of_any_flavour_reads_into_a_DateTime(string type)
        {
            var rows = await Raw<ForeignDateTime>($"SELECT CAST('2026-09-27 13:14:00' AS {type}) AS V, CAST('2026-09-27 13:14:00' AS {type}) AS R");

            Assert.Equal(new DateTime(2026, 9, 27, 13, 14, 0), Assert.Single(rows).V);
            Assert.Equal(new DateTime(2026, 9, 27, 13, 14, 0), rows[0].R);
        }

        [Theory]
        [InlineData("money")]
        [InlineData("smallmoney")]
        [InlineData("numeric(10,2)")]
        [InlineData("decimal(10,2)")]
        public async Task A_money_or_numeric_reads_into_a_decimal(string type)
        {
            var rows = await Raw<ForeignDecimal>($"SELECT CAST(12.34 AS {type}) AS V, CAST(-56.78 AS {type}) AS R");

            Assert.Equal(12.34m, Assert.Single(rows).V);
            Assert.Equal(-56.78m, rows[0].R);
        }

        [Fact]
        public async Task A_bit_reads_into_a_nullable_and_a_required_bool_and_a_null_bit_into_null()
        {
            var rows = await Raw<ForeignBool>("SELECT CAST(1 AS bit) AS V, CAST(0 AS bit) AS R UNION ALL SELECT NULL, CAST(1 AS bit)");

            Assert.Equal(2, rows.Count);
            Assert.True(rows[0].V);
            Assert.False(rows[0].R);
            Assert.Null(rows[1].V);
            Assert.True(rows[1].R);
        }

        [Fact]
        public async Task An_int_column_reads_into_an_enum_but_a_tinyint_does_not()
        {
            var ok = await Raw<ForeignKind>("SELECT CAST(1 AS int) AS V, CAST(1000 AS int) AS R");
            Assert.Equal(MatrixKind.One, Assert.Single(ok).V);
            Assert.Equal(MatrixKind.Big, ok[0].R);

            //The reader asks for an int, and SqlClient's GetInt32 does not widen a tinyint.
            await Assert.ThrowsAsync<InvalidCastException>(() => Raw<ForeignKind>("SELECT CAST(1 AS tinyint) AS V, CAST(1 AS int) AS R"));
            await Assert.ThrowsAsync<InvalidCastException>(() => Raw<ForeignKind>("SELECT CAST(1 AS int) AS V, CAST(1 AS tinyint) AS R"));
        }

        [Fact]
        public async Task Columns_of_exactly_the_property_type_read_and_narrower_ones_do_not_widen()
        {
            //The generated reader casts the boxed value, so the column has to be the property's own
            //type: a tinyint is not read into an int, a smallint into an int, or an int into a long.
            Assert.Equal(7, Assert.Single(await Raw<ForeignInt>("SELECT CAST(7 AS int) AS V, CAST(7 AS int) AS R")).V);
            Assert.Equal(7L, Assert.Single(await Raw<ForeignLong>("SELECT CAST(7 AS bigint) AS V, CAST(7 AS bigint) AS R")).V);

            await Assert.ThrowsAsync<InvalidCastException>(() => Raw<ForeignInt>("SELECT CAST(7 AS tinyint) AS V, CAST(7 AS int) AS R"));
            await Assert.ThrowsAsync<InvalidCastException>(() => Raw<ForeignInt>("SELECT CAST(7 AS smallint) AS V, CAST(7 AS int) AS R"));
            await Assert.ThrowsAsync<InvalidCastException>(() => Raw<ForeignLong>("SELECT CAST(7 AS int) AS V, CAST(7 AS bigint) AS R"));
            await Assert.ThrowsAsync<InvalidCastException>(() => Raw<ForeignLong>("SELECT CAST(7 AS bigint) AS V, CAST(7 AS int) AS R"));
        }

        [Fact]
        public async Task A_nested_public_class_is_read_like_any_other()
        {
            //The generated reader resolves the entity by its name, and a nested class is not
            //Namespace.Name: this is the case that failed while KeyAndJsonFixTests was written.
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(NestedRow), CancellationToken.None);
            await connection.CreateTable<NestedRow>();
            try
            {
                await connection.Insert(new NestedRow { Label = "nested" });

                Assert.Equal("nested", Assert.Single(await connection.Query<NestedRow>()).Label);
            }
            finally
            {
                await connection.DropTableIfExists(typeof(NestedRow), CancellationToken.None);
            }
        }

        [Table("NestedRow")]
        public sealed class NestedRow
        {
            [Column(Name = "Id", Key = KeyType.Key)]
            public int Id { get; set; }

            [Column(Name = "Label", Length = 20)]
            public string Label { get; set; } = string.Empty;
        }
    }

    [Table("ForeignString")]
    public sealed class ForeignString
    {
        [Column(Name = "V")]
        public string? V { get; set; }

        [Column(Name = "R", Required = true)]
        public string R { get; set; } = string.Empty;
    }

    [Table("ForeignDateTime")]
    public sealed class ForeignDateTime
    {
        [Column(Name = "V")]
        public DateTime V { get; set; }

        [Column(Name = "R", Required = true)]
        public DateTime R { get; set; }
    }

    [Table("ForeignDecimal")]
    public sealed class ForeignDecimal
    {
        [Column(Name = "V", Length = 18, Decimals = 2)]
        public decimal V { get; set; }

        [Column(Name = "R", Required = true, Length = 18, Decimals = 2)]
        public decimal R { get; set; }
    }

    [Table("ForeignBool")]
    public sealed class ForeignBool
    {
        [Column(Name = "V")]
        public bool? V { get; set; }

        [Column(Name = "R", Required = true)]
        public bool R { get; set; }
    }

    [Table("ForeignKind")]
    public sealed class ForeignKind
    {
        [Column(Name = "V")]
        public MatrixKind V { get; set; }

        [Column(Name = "R", Required = true)]
        public MatrixKind R { get; set; }
    }

    [Table("ForeignInt")]
    public sealed class ForeignInt
    {
        [Column(Name = "V")]
        public int V { get; set; }

        [Column(Name = "R", Required = true)]
        public int R { get; set; }
    }

    [Table("ForeignLong")]
    public sealed class ForeignLong
    {
        [Column(Name = "V")]
        public long V { get; set; }

        [Column(Name = "R", Required = true)]
        public long R { get; set; }
    }
}

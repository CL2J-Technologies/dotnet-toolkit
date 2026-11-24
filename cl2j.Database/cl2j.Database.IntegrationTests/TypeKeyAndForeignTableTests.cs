using cl2j.Database.DataAnnotations;
using cl2j.Database.TypeMatrix;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     SQL Server's run of the shared <see cref="TypeKeyAndForeignTableTestsBase"/> (the keys of
    ///     each type, and the nested class), plus what stays here because it is SQL Server's own:
    ///     tables the library did not create, read through raw SQL. This half only reports what the
    ///     reader does with column types it never declared: the mapping is by exact type and is not
    ///     extended here. The casts (<c>CAST(... AS nvarchar(50))</c>, <c>money</c>, <c>tinyint</c>)
    ///     are SQL Server's types, and the <see cref="InvalidCastException"/>s are the exact
    ///     conversions SqlClient's getters make.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class TypeKeyAndForeignTableTests(SqlServerFixture fixture) : TypeKeyAndForeignTableTestsBase(fixture)
    {
        //Raw SQL over columns of types the library did not create. CAST gives the server type
        //without a table.
        private async Task<List<T>> Raw<T>(string select)
        {
            await using var connection = await Fixture.OpenAsync();
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

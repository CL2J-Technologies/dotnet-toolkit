using System.Data.Common;
using cl2j.Database.DataAnnotations;
using cl2j.Database.TypeMatrix;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     The values at the edge of what each type can hold, and what happens at the edge: where
    ///     the library keeps the value, where it refuses it, and where the server changes it and
    ///     says nothing. The cases that change a value are pinned as they are so that a change in
    ///     behaviour is a visible one; the README lists them under "Known limitations".
    ///
    ///     <para>
    ///     What differs per provider goes through the hooks of <see cref="TypeTestsBase"/> (the
    ///     temporal precision, a value that is replaced or that comes back changed) and through
    ///     <see cref="StoresNonFiniteDoubles"/> and <see cref="RefusesTimeSpanOutsideADay"/>.
    ///     The tests of characters outside the code page of a <c>char</c> or <c>varchar</c>
    ///     column are not here: they need <c>nchar</c> and <c>nvarchar</c> columns, which are SQL
    ///     Server's names for a type another server may not have, so they stay with its tests.
    ///     </para>
    ///
    ///     <para>
    ///     Not hooked because the spikes show no difference, but unproven off SQL Server: the
    ///     decimal tests assume a <c>decimal(38,28)</c> column, the float test stores
    ///     <see cref="float.Epsilon"/> and <see cref="double.Epsilon"/> (denormals, which a server
    ///     may flush to zero), and the <see cref="DateTime"/> test writes the three kinds.
    ///     </para>
    /// </summary>
    public abstract class TypeEdgeValueTestsBase(IDatabaseFixture fixture) : TypeTestsBase(fixture)
    {
        /// <summary>
        ///     Whether a <c>double</c> column holds NaN and the two infinities. SQL Server:
        ///     <see langword="false"/>, its <c>float</c> has none, so the insert is refused and
        ///     nothing is stored. Override with <see langword="true"/> where the server stores
        ///     them (PostgreSQL's <c>double precision</c> does); the test then expects all three
        ///     back, through <c>Insert</c> and <c>BulkInsert</c>. A server that refuses them
        ///     (MySQL's <c>double</c>) keeps the default.
        /// </summary>
        protected virtual bool StoresNonFiniteDoubles => false;

        /// <summary>
        ///     Whether a <see cref="TimeSpan"/> of a day or more, or a negative one, is refused.
        ///     SQL Server: <see langword="true"/>, its <c>time</c> is a time of day
        ///     (<c>00:00:00</c> to <c>23:59:59.9999999</c>). Override with <see langword="false"/>
        ///     where the column is a duration: PostgreSQL's <c>interval</c> and MySQL's
        ///     <c>time</c> (up to 838 hours, signed) keep 25 hours and minus one second, which the
        ///     test then expects back.
        /// </summary>
        protected virtual bool RefusesTimeSpanOutsideADay => true;

        /// <summary>
        ///     Writes <paramref name="row"/> through <c>Insert</c> and through <c>BulkInsert</c>,
        ///     each into a fresh table, and returns what each read back. For a derived class that
        ///     keeps provider-only cases of its own, on an entity of its own.
        /// </summary>
        protected async Task<(T ReadByInsert, T ReadByBulk)> BothWays<T>(T row) where T : class, new()
        {
            await using var connection = await Fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(T), CancellationToken.None);
            await connection.CreateTable<T>();
            T inserted;
            try
            {
                var id = await connection.Insert<T, int>(row);
                inserted = (await connection.QueryKey<T>(id))!;
            }
            finally
            {
                await connection.DropTableIfExists(typeof(T), CancellationToken.None);
            }

            await connection.CreateTable<T>();
            try
            {
                await connection.BulkInsert(new[] { row });
                return (inserted, Assert.Single(await connection.Query<T>()));
            }
            finally
            {
                await connection.DropTableIfExists(typeof(T), CancellationToken.None);
            }
        }

        private Task<(EdgeRow ReadByInsert, EdgeRow ReadByBulk)> BothWays(EdgeRow row) => BothWays<EdgeRow>(row);

        [Fact]
        public async Task A_decimal_keeps_every_digit_at_the_widest_precision_a_decimal_holds()
        {
            var row = new EdgeRow { Wide = decimal.MaxValue, WideScaled = 1234567890.123456789012345678m, Tiny = 0.0000000000000000000000000001m };

            var (inserted, bulk) = await BothWays(row);

            foreach (var read in new[] { inserted, bulk })
            {
                Assert.Equal(decimal.MaxValue, read.Wide);
                Assert.Equal(1234567890.123456789012345678m, read.WideScaled);
                Assert.Equal(0.0000000000000000000000000001m, read.Tiny);
            }
        }

        [Fact]
        public async Task A_decimal_below_the_widest_value_survives_too()
        {
            var (inserted, bulk) = await BothWays(new EdgeRow { Wide = decimal.MinValue });

            Assert.Equal(decimal.MinValue, inserted.Wide);
            Assert.Equal(decimal.MinValue, bulk.Wide);
        }

        [Fact]
        public async Task A_decimal_with_more_decimals_than_its_column_is_rounded_by_the_server()
        {
            //Pinned, not endorsed: the declared Decimals is the column's, and SQL Server converts
            //the parameter to it with rounding and no error.
            var (inserted, _) = await BothWays(new EdgeRow { Money = 1234.567m });

            Assert.Equal(1234.57m, inserted.Money);
        }

        [Fact]
        public async Task A_decimal_over_the_declared_precision_is_refused_by_the_server()
        {
            await using var connection = await Fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(EdgeRow), CancellationToken.None);
            await connection.CreateTable<EdgeRow>();
            try
            {
                //decimal(18,2) holds 16 integer digits.
                await Assert.ThrowsAnyAsync<Exception>(() => connection.Insert(new EdgeRow { Money = 12345678901234567m }));
            }
            finally
            {
                await connection.DropTableIfExists(typeof(EdgeRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task An_empty_string_and_a_null_string_stay_apart()
        {
            var (inserted, bulk) = await BothWays(new EdgeRow { Empty = string.Empty, Absent = null });

            foreach (var read in new[] { inserted, bulk })
            {
                Assert.Equal(string.Empty, read.Empty);
                Assert.Null(read.Absent);
            }
        }

        [Fact]
        public async Task An_empty_byte_array_and_a_null_one_stay_apart()
        {
            var (inserted, bulk) = await BothWays(new EdgeRow { EmptyBytes = [], AbsentBytes = null });

            foreach (var read in new[] { inserted, bulk })
            {
                Assert.NotNull(read.EmptyBytes);
                Assert.Empty(read.EmptyBytes);
                Assert.Null(read.AbsentBytes);
            }
        }

        [Fact]
        public async Task A_string_and_a_byte_array_beyond_the_common_size_are_kept_whole()
        {
            var text = new string('a', 20_000);
            var bytes = Enumerable.Range(0, 20_000).Select(i => (byte)i).ToArray();

            var (inserted, bulk) = await BothWays(new EdgeRow { LongString = text, LongBytes = bytes });

            foreach (var read in new[] { inserted, bulk })
            {
                Assert.Equal(text, read.LongString);
                Assert.Equal(bytes, read.LongBytes);
            }
        }

        [Fact]
        public async Task An_enum_value_that_is_not_defined_is_kept()
        {
            var (inserted, bulk) = await BothWays(new EdgeRow { Kind = (MatrixKind)42, MaybeKind = (MatrixKind)(-1) });

            foreach (var read in new[] { inserted, bulk })
            {
                Assert.Equal((MatrixKind)42, read.Kind);
                Assert.Equal((MatrixKind)(-1), read.MaybeKind);
            }
        }

        [Fact]
        public async Task A_DateTimeOffset_keeps_its_offset_and_the_instant()
        {
            var moment = Written(new DateTimeOffset(2026, 9, 27, 13, 14, 15, TimeSpan.FromMinutes(-210)).AddTicks(1234567));
            var expected = Expected(moment);

            var (inserted, bulk) = await BothWays(new EdgeRow { Moment = moment });

            foreach (var read in new[] { inserted, bulk })
            {
                //Equality of DateTimeOffset is the instant; the offset is asserted on its own.
                Assert.Equal(expected, read.Moment);
                Assert.Equal(expected.Offset, read.Moment.Offset);
            }
        }

        [Fact]
        public async Task A_DateTimeOffset_with_the_extreme_offsets_is_kept()
        {
            var moment = Written(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(14)));
            var expected = Expected(moment);

            var (inserted, bulk) = await BothWays(new EdgeRow { Moment = moment });

            Assert.Equal(expected.Offset, inserted.Moment.Offset);
            Assert.Equal(expected.Offset, bulk.Moment.Offset);
            Assert.Equal(expected, inserted.Moment);
            Assert.Equal(expected, bulk.Moment);
        }

        [Theory]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public async Task A_DateTime_keeps_its_ticks_and_loses_its_kind(DateTimeKind kind)
        {
            //datetime2 stores no kind: the value comes back Unspecified, with the ticks it went in
            //with and no time zone conversion.
            var stamp = Written(new DateTime(2026, 9, 27, 13, 14, 15, kind).AddTicks(1234567));
            var expected = Expected(stamp);

            var (inserted, bulk) = await BothWays(new EdgeRow { Stamp = stamp });

            foreach (var read in new[] { inserted, bulk })
            {
                Assert.Equal(expected.Ticks, read.Stamp.Ticks);
                Assert.Equal(DateTimeKind.Unspecified, read.Stamp.Kind);
            }
        }

        [Fact]
        public async Task A_double_keeps_all_of_its_digits_and_a_float_keeps_its_own()
        {
            var (inserted, bulk) = await BothWays(new EdgeRow { Precise = 1.0000000000000002d, Tiny32 = float.Epsilon, Small64 = double.Epsilon, Real32 = 16777217f });

            foreach (var read in new[] { inserted, bulk })
            {
                Assert.Equal(1.0000000000000002d, read.Precise);
                Assert.Equal(float.Epsilon, read.Tiny32);
                Assert.Equal(double.Epsilon, read.Small64);
                //float has 24 bits of mantissa: 16777217 is already 16777216 in the .NET value.
                Assert.Equal(16777216f, read.Real32);
            }
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public async Task A_double_that_is_not_a_finite_number_is_refused_not_stored(double value)
        {
            if (StoresNonFiniteDoubles)
            {
                var (inserted, bulk) = await BothWays(new EdgeRow { Precise = value });

                Assert.Equal(value, inserted.Precise);
                Assert.Equal(value, bulk.Precise);
                return;
            }

            //SQL Server's float has no NaN and no infinity.
            await using var connection = await Fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(EdgeRow), CancellationToken.None);
            await connection.CreateTable<EdgeRow>();
            try
            {
                await Assert.ThrowsAnyAsync<Exception>(() => connection.Insert(new EdgeRow { Precise = value }));
                await Assert.ThrowsAnyAsync<Exception>(() => connection.BulkInsert(new[] { new EdgeRow { Precise = value } }));
                Assert.Empty(await connection.Query<EdgeRow>());
            }
            finally
            {
                await connection.DropTableIfExists(typeof(EdgeRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task A_TimeSpan_of_a_day_or_more_is_refused_not_truncated()
        {
            if (!RefusesTimeSpanOutsideADay)
            {
                var (inserted, bulk) = await BothWays(new EdgeRow { Elapsed = TimeSpan.FromHours(25) });
                Assert.Equal(TimeSpan.FromHours(25), inserted.Elapsed);
                Assert.Equal(TimeSpan.FromHours(25), bulk.Elapsed);

                (inserted, bulk) = await BothWays(new EdgeRow { Elapsed = TimeSpan.FromSeconds(-1) });
                Assert.Equal(TimeSpan.FromSeconds(-1), inserted.Elapsed);
                Assert.Equal(TimeSpan.FromSeconds(-1), bulk.Elapsed);
                return;
            }

            //SQL Server's time is a time of day: 00:00:00 to 23:59:59.9999999. A longer duration
            //has no representation, so it must fail rather than wrap around.
            await using var connection = await Fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(EdgeRow), CancellationToken.None);
            await connection.CreateTable<EdgeRow>();
            try
            {
                await Assert.ThrowsAnyAsync<Exception>(() => connection.Insert(new EdgeRow { Elapsed = TimeSpan.FromHours(25) }));
                await Assert.ThrowsAnyAsync<Exception>(() => connection.Insert(new EdgeRow { Elapsed = TimeSpan.FromSeconds(-1) }));
                await Assert.ThrowsAnyAsync<Exception>(() => connection.BulkInsert(new[] { new EdgeRow { Elapsed = TimeSpan.FromHours(25) } }));
                Assert.Empty(await connection.Query<EdgeRow>());
            }
            finally
            {
                await connection.DropTableIfExists(typeof(EdgeRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task A_TimeSpan_just_under_a_day_is_kept()
        {
            var last = Written(TimeSpan.FromDays(1).Subtract(TimeSpan.FromTicks(1)));
            var expected = Expected(last);

            var (inserted, bulk) = await BothWays(new EdgeRow { Elapsed = last });

            Assert.Equal(expected, inserted.Elapsed);
            Assert.Equal(expected, bulk.Elapsed);
        }

        [Fact]
        public async Task The_extremes_of_every_integer_type_are_kept()
        {
            var row = new EdgeRow { Tiny8 = byte.MaxValue, Small16 = short.MinValue, Int32Value = int.MinValue, Long64 = long.MinValue };
            var (inserted, bulk) = await BothWays(row);

            foreach (var read in new[] { inserted, bulk })
            {
                Assert.Equal(byte.MaxValue, read.Tiny8);
                Assert.Equal(short.MinValue, read.Small16);
                Assert.Equal(int.MinValue, read.Int32Value);
                Assert.Equal(long.MinValue, read.Long64);
            }
        }
    }

    /// <summary>
    ///     One table with the columns the edge cases need, so that each test reads one row back and
    ///     checks only the column it is about. Holds nothing that needs a provider's own type
    ///     name; the national-character columns are SQL Server's and live with its tests.
    /// </summary>
    [Table("EdgeValues")]
    public sealed class EdgeRow
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "Wide", Length = 38, Decimals = 0)]
        public decimal Wide { get; set; }

        [Column(Name = "WideScaled", Length = 38, Decimals = 18)]
        public decimal WideScaled { get; set; }

        [Column(Name = "Tiny", Length = 38, Decimals = 28)]
        public decimal Tiny { get; set; }

        [Column(Name = "Money", Length = 18, Decimals = 2)]
        public decimal Money { get; set; }

        [Column(Name = "Empty", Length = 10)]
        public string? Empty { get; set; }

        [Column(Name = "Absent", Length = 10)]
        public string? Absent { get; set; }

        [Column(Name = "EmptyBytes")]
        public byte[]? EmptyBytes { get; set; }

        [Column(Name = "AbsentBytes")]
        public byte[]? AbsentBytes { get; set; }

        [Column(Name = "LongText")]
        public string? LongString { get; set; }

        [Column(Name = "LongBytes")]
        public byte[]? LongBytes { get; set; }

        [Column(Name = "Kind")]
        public MatrixKind Kind { get; set; }

        [Column(Name = "MaybeKind")]
        public MatrixKind? MaybeKind { get; set; }

        [Column(Name = "Moment")]
        public DateTimeOffset Moment { get; set; } = DateTimeOffset.UnixEpoch;

        [Column(Name = "Stamp")]
        public DateTime Stamp { get; set; } = new DateTime(2000, 1, 1);

        [Column(Name = "Precise")]
        public double Precise { get; set; }

        [Column(Name = "Tiny32")]
        public float Tiny32 { get; set; }

        [Column(Name = "Small64")]
        public double Small64 { get; set; }

        [Column(Name = "Single")]
        public float Real32 { get; set; }

        [Column(Name = "Elapsed")]
        public TimeSpan Elapsed { get; set; }

        [Column(Name = "Tiny8")]
        public byte Tiny8 { get; set; }

        [Column(Name = "Short")]
        public short Small16 { get; set; }

        [Column(Name = "Int")]
        public int Int32Value { get; set; }

        [Column(Name = "Long64")]
        public long Long64 { get; set; }
    }
}

using System.Data.Common;
using System.Globalization;
using MySqlConnector;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    /// <summary>
    ///     Where MySQL's behaviour differs from SQL Server's and the shared suite cannot say so:
    ///     each test pins what the server and the driver do, and docs/providers/mysql.md quotes
    ///     them. Not endorsements: a "characterisation" test is there so that a change of server or
    ///     driver that alters the behaviour is noticed.
    /// </summary>
    [Collection(MySqlTestGroup.Name)]
    public class MySqlBehaviourTests(MySqlFixture fixture) : IAsyncLifetime
    {
        private static readonly Type[] Tables =
        [
            typeof(MyKeyedRow), typeof(MyTooWideRow), typeof(MyTooLongColumnRow), typeof(MyUnboundedRow),
            typeof(MyShortRow), typeof(MyBatchRow), typeof(MyTemporalRow),
        ];

        public async Task InitializeAsync() => await DropAll();

        public async Task DisposeAsync() => await DropAll();

        private async Task DropAll()
        {
            await using var connection = await fixture.OpenAsync();
            foreach (var table in Tables)
                await connection.DropTableIfExists(table, CancellationToken.None);
        }

        private static async Task<string> Raw(DbConnection connection, string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) ?? "<null>";
        }

        //The three ways a row gets in: a parameterised INSERT, the batched multi-row INSERT and
        //MySqlBulkCopy (the connection string allows LOAD DATA LOCAL INFILE).
        public static TheoryData<string> Writes => new() { "insert", "batched", "bulkcopy" };

        private async Task Write<T>(string how, T row) where T : class
        {
            await using var connection = how == "bulkcopy" ? await fixture.BulkCopy.OpenAsync() : await fixture.OpenAsync();
            if (how == "insert")
                await connection.Insert(row);
            else
                await connection.BulkInsert([row]);
        }

        #region Offset and precision

        [Theory]
        [MemberData(nameof(Writes))]
        public async Task A_DateTimeOffset_is_stored_as_the_utc_instant_and_comes_back_with_offset_zero(string how)
        {
            //MySqlConnector converts a DateTimeOffset parameter to UTC, and MySqlBulkCopy and the
            //batched INSERT (a parameter too) do the same: the three paths agree with the reader,
            //which takes a datetime as UTC. The instant is right; the -05:00 is gone.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<MyTemporalRow>();
            var moment = new DateTimeOffset(2026, 11, 24, 8, 0, 0, TimeSpan.FromHours(-5));

            await Write(how, new MyTemporalRow { Id = "r", Moment = moment, At = new DateTime(2026, 11, 24, 8, 0, 0) });

            Assert.Equal("2026-11-24 13:00:00.000000", await Raw(connection, "SELECT CAST(`Moment` AS CHAR) FROM `My_Temporal`"));
            var read = await connection.QueryKey<MyTemporalRow>("r");
            Assert.Equal(TimeSpan.Zero, read!.Moment.Offset);
            Assert.Equal(moment, read.Moment);
        }

        [Fact]
        public async Task An_update_converts_the_offset_the_same_way()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<MyTemporalRow>();
            await connection.Insert(new MyTemporalRow { Id = "r", Moment = DateTimeOffset.UnixEpoch, At = new DateTime(2026, 1, 1) });

            await connection.Update(new MyTemporalRow { Id = "r", Moment = new DateTimeOffset(2026, 11, 24, 8, 0, 0, TimeSpan.FromHours(3)), At = new DateTime(2026, 1, 1) });

            Assert.Equal("2026-11-24 05:00:00.000000", await Raw(connection, "SELECT CAST(`Moment` AS CHAR) FROM `My_Temporal`"));
        }

        [Theory]
        [MemberData(nameof(Writes))]
        public async Task The_seventh_fractional_digit_is_truncated_not_rounded(string how)
        {
            //datetime(6) stops at the microsecond. The driver cuts the tick count, it does not round:
            //.9999999 stays .999999 instead of carrying into the next second. The same for the
            //parameter (DbType.DateTime2, which the core binds every DateTime with) and both bulk
            //paths.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<MyTemporalRow>();
            var at = new DateTime(2026, 11, 24, 13, 14, 15).AddTicks(9_999_999);

            await Write(how, new MyTemporalRow { Id = "r", At = at, Moment = DateTimeOffset.UnixEpoch });

            Assert.Equal("2026-11-24 13:14:15.999999", await Raw(connection, "SELECT CAST(`At` AS CHAR) FROM `My_Temporal`"));
            var read = await connection.QueryKey<MyTemporalRow>("r");
            Assert.Equal(at.AddTicks(-9), read!.At);
        }

        [Theory]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        [InlineData(DateTimeKind.Unspecified)]
        public async Task The_kind_of_a_DateTime_is_ignored_not_converted(DateTimeKind kind)
        {
            //The value is written as the wall clock it carries, whatever its Kind says, and comes
            //back Unspecified. A Local value is not moved to UTC: convert it before writing.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<MyTemporalRow>();

            await connection.Insert(new MyTemporalRow { Id = "r", At = new DateTime(2026, 11, 24, 8, 0, 0, kind), Moment = DateTimeOffset.UnixEpoch });

            Assert.Equal("2026-11-24 08:00:00.000000", await Raw(connection, "SELECT CAST(`At` AS CHAR) FROM `My_Temporal`"));
            Assert.Equal(DateTimeKind.Unspecified, (await connection.QueryKey<MyTemporalRow>("r"))!.At.Kind);
        }

        #endregion

        #region Collation

        [Fact]
        public async Task The_default_collation_is_accent_and_case_insensitive()
        {
            //MySQL 8's default is utf8mb4_0900_ai_ci. SQL Server's usual default is case-insensitive
            //but accent-sensitive.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<MyKeyedRow>();

            Assert.Equal("utf8mb4_0900_ai_ci", await Raw(connection,
                "SELECT COLLATION_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'My_Keyed' AND COLUMN_NAME = 'Code'"));
        }

        [Fact]
        public async Task String_keys_that_differ_only_by_accent_or_case_collide()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<MyKeyedRow>();
            await connection.Insert(new MyKeyedRow { Code = "Renée", Label = "accented" });

            var ex = await Assert.ThrowsAsync<MySqlException>(() => connection.Insert(new MyKeyedRow { Code = "RENEE", Label = "plain" }));

            Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, ex.ErrorCode);
        }

        [Theory]
        [InlineData("RENEE")]
        [InlineData("renee")]
        [InlineData("Renée")]
        public async Task A_lookup_by_key_matches_whatever_the_case_or_accent(string lookup)
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<MyKeyedRow>();
            await connection.Insert(new MyKeyedRow { Code = "Renée", Label = "accented" });

            Assert.Equal("accented", (await connection.QueryKey<MyKeyedRow>(lookup))!.Label);
            Assert.Equal("accented", Assert.Single(await connection.QueryKeys<MyKeyedRow>([lookup])).Label);
        }

        [Fact]
        public async Task A_query_by_example_matches_whatever_the_case_or_accent()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<MyKeyedRow>();
            await connection.Insert(new MyKeyedRow { Code = "a", Label = "Ça va" });

            var found = await connection.Query<MyKeyedRow, MyKeyedRow>(new MyKeyedRow { Label = "CA VA" });

            Assert.Equal("a", Assert.Single(found).Code);
        }

        #endregion

        #region Size limits

        [Fact]
        public async Task Two_varchar_columns_over_the_65535_byte_row_limit_are_refused_when_the_table_is_created()
        {
            //varchar(n) counts n * 4 bytes in utf8mb4 against a row limit of 65,535 bytes. SQL Server
            //has no such limit on varchar(n), so a model that works there can fail here, at
            //CreateTable. Two varchar(10000) columns are 80,000 bytes.
            await using var connection = await fixture.OpenAsync();

            var ex = await Assert.ThrowsAsync<MySqlException>(() => connection.CreateTable<MyTooWideRow>());

            Assert.Equal(MySqlErrorCode.TooBigRowSize, ex.ErrorCode);
            Assert.False(await connection.TableExists<MyTooWideRow>());
        }

        [Fact]
        public async Task A_single_varchar_over_16383_characters_is_refused_when_the_table_is_created()
        {
            await using var connection = await fixture.OpenAsync();

            var ex = await Assert.ThrowsAsync<MySqlException>(() => connection.CreateTable<MyTooLongColumnRow>());

            Assert.Equal(MySqlErrorCode.TooBigFieldlength, ex.ErrorCode);
        }

        [Fact]
        public async Task Unbounded_strings_are_longtext_and_do_not_count_against_the_row_limit()
        {
            //A string with no Length is longtext, which the row stores out of line (about nine bytes
            //each). That is how a model avoids the limit above, and why the library does not map an
            //unbounded string to a large varchar.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<MyUnboundedRow>();
            var big = new string('é', 1_000_000);

            await connection.Insert(new MyUnboundedRow { Id = "r", First = big, Second = big, Third = big, Fourth = big });

            var read = await connection.QueryKey<MyUnboundedRow>("r");
            Assert.Equal(big, read!.Fourth);
            Assert.Equal("longtext", await Raw(connection,
                "SELECT DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'My_Unbounded' AND COLUMN_NAME = 'First'"));
        }

        [Theory]
        [InlineData("insert")]
        [InlineData("batched")]
        public async Task A_value_too_long_for_its_varchar_is_refused_under_the_default_strict_mode(string how)
        {
            //STRICT_TRANS_TABLES is on by default (and in the test server): the server rejects the row,
            //it does not cut the value to fit.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<MyShortRow>();

            var ex = await Assert.ThrowsAsync<MySqlException>(() => Write(how, new MyShortRow { Id = "r", Label = "toolong" }));

            Assert.Equal(MySqlErrorCode.DataTooLong, ex.ErrorCode);
            Assert.Empty(await connection.Query<MyShortRow>());
        }

        [Fact]
        public async Task A_value_too_long_for_its_varchar_is_refused_by_the_bulk_copy_too_though_the_server_would_truncate_it()
        {
            //LOAD DATA LOCAL INFILE implies IGNORE: the server cuts the value to five characters and
            //raises a warning, where an INSERT fails. MySqlBulkCopy does not look at the warning;
            //the provider does, and refuses (and rolls back), so that all three paths agree.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<MyShortRow>();

            var ex = await Assert.ThrowsAsync<Exceptions.DatabaseException>(() => Write("bulkcopy", new MyShortRow { Id = "r", Label = "toolong" }));

            Assert.Contains("Data truncated for column 'Label'", ex.Message, StringComparison.Ordinal);
            Assert.Empty(await connection.Query<MyShortRow>());
        }

        #endregion

        #region Range

        [Theory]
        [MemberData(nameof(Writes))]
        public async Task Dates_below_the_documented_year_1000_minimum_are_stored_and_read_back_on_8_4(string how)
        {
            //MySQL documents 1000-01-01 as the first supported datetime and date, and warns that
            //earlier values may work but are not guaranteed. On the 8.4 server the suite runs
            //against, year 1 (DateTime.MinValue) and 999-12-31 round-trip through all three paths,
            //so the provider adds no clamp. Do not rely on it across servers: stay at 1000 or later.
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<MyTemporalRow>();
            var first = DateTime.MinValue;

            await Write(how, new MyTemporalRow { Id = "r", At = first, Moment = new DateTimeOffset(first, TimeSpan.Zero), Day = DateOnly.MinValue });

            var read = await connection.QueryKey<MyTemporalRow>("r");
            Assert.Equal(first, read!.At);
            Assert.Equal(first, read.Moment.UtcDateTime);
            Assert.Equal(DateOnly.MinValue, read.Day);
            Assert.Equal("0001-01-01 00:00:00.000000", await Raw(connection, "SELECT CAST(`At` AS CHAR) FROM `My_Temporal`"));
        }

        [Theory]
        [MemberData(nameof(Writes))]
        public async Task The_last_representable_instant_keeps_six_digits_of_its_seventh_truncated(string how)
        {
            await using var connection = await fixture.OpenAsync();
            await connection.CreateTable<MyTemporalRow>();

            await Write(how, new MyTemporalRow { Id = "r", At = DateTime.MaxValue, Moment = DateTimeOffset.MaxValue, Day = DateOnly.MaxValue });

            Assert.Equal("9999-12-31 23:59:59.999999", await Raw(connection, "SELECT CAST(`At` AS CHAR) FROM `My_Temporal`"));
            Assert.Equal(DateOnly.MaxValue, (await connection.QueryKey<MyTemporalRow>("r"))!.Day);
        }

        #endregion
    }
}

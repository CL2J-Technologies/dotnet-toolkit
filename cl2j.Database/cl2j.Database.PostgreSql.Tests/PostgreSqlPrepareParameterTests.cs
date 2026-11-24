using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace cl2j.Database.PostgreSql.Tests
{
    /// <summary>
    ///     The three adjustments Npgsql needs before a value travels: UTC for a
    ///     <see cref="DateTimeOffset"/>, an unspecified kind for a <see cref="DateTime"/> bound to
    ///     <c>timestamp</c>, and a JSON type for a JSON column. Each is a branch with its own way of
    ///     going wrong, and a server round trip only sees the ones that fail loudly.
    /// </summary>
    public class PostgreSqlPrepareParameterTests
    {
        [Table("Prepared")]
        private sealed class PreparedRow
        {
            [Column(Name = "Id", Key = KeyType.Key, Length = 10)]
            public string Id { get; set; } = string.Empty;

            [Column(Name = "Plain", Json = true)]
            public List<int>? Plain { get; set; }

            [Column(Name = "Declared", Json = true, TypeName = "json")]
            public List<int>? Declared { get; set; }

            [Column(Name = "DeclaredBinary", Json = true, TypeName = "jsonb")]
            public List<int>? DeclaredBinary { get; set; }

            [Column(Name = "Capped", Json = true, Length = 200)]
            public List<int>? Capped { get; set; }

            [Column(Name = "Text", Length = 20)]
            public string? Text { get; set; }

            [Column(Name = "Stamp")]
            public DateTime Stamp { get; set; }

            [Column(Name = "Moment")]
            public DateTimeOffset Moment { get; set; }
        }

        private static ICommandBuilder Builder()
        {
            cl2j.Database.PostgreSql.PostgreSql.Register();
            return CommandBuilderFactory.GetCommandBuilder(new NpgsqlConnection());
        }

        private static ColumnDescriptor Column(string name)
            => TableDescriptorFactory.Create(typeof(PreparedRow), Builder().DatabaseFormatter).Columns.Single(c => c.Name == name);

        private static NpgsqlParameter Prepare(object? value, string? column)
        {
            var parameter = new NpgsqlParameter("p", value);
            Builder().PrepareParameter(parameter, column is null ? null : Column(column));
            return parameter;
        }

        [Fact]
        public void A_DateTimeOffset_is_sent_as_the_same_instant_in_UTC()
        {
            //Npgsql refuses a DateTimeOffset whose offset is not zero for timestamptz.
            var moment = new DateTimeOffset(2026, 11, 24, 13, 0, 0, TimeSpan.FromHours(5));

            var parameter = Prepare(moment, "Moment");

            var sent = Assert.IsType<DateTimeOffset>(parameter.Value);
            Assert.Equal(TimeSpan.Zero, sent.Offset);
            Assert.Equal(moment, sent);
        }

        [Fact]
        public void A_DateTimeOffset_is_adjusted_without_a_column_too()
        {
            var parameter = Prepare(new DateTimeOffset(2026, 11, 24, 13, 0, 0, TimeSpan.FromHours(-8)), null);

            Assert.Equal(TimeSpan.Zero, Assert.IsType<DateTimeOffset>(parameter.Value).Offset);
        }

        [Theory]
        [InlineData(DateTimeKind.Utc)]
        [InlineData(DateTimeKind.Local)]
        public void A_DateTime_of_a_specified_kind_loses_the_kind_and_keeps_the_ticks(DateTimeKind kind)
        {
            //Npgsql refuses a Utc value for timestamp, which holds no zone.
            var at = new DateTime(2026, 11, 24, 13, 14, 15, 123, kind);

            var parameter = Prepare(at, "Stamp");

            var sent = Assert.IsType<DateTime>(parameter.Value);
            Assert.Equal(DateTimeKind.Unspecified, sent.Kind);
            Assert.Equal(at.Ticks, sent.Ticks);
        }

        [Fact]
        public void An_unspecified_DateTime_is_left_as_it_is()
        {
            var at = new DateTime(2026, 11, 24, 13, 14, 15, DateTimeKind.Unspecified);

            var parameter = Prepare(at, "Stamp");

            Assert.Equal(at, parameter.Value);
        }

        [Theory]
        [InlineData("Plain", NpgsqlDbType.Jsonb)]
        [InlineData("Declared", NpgsqlDbType.Json)]
        [InlineData("DeclaredBinary", NpgsqlDbType.Jsonb)]
        public void A_json_column_binds_its_string_as_the_json_type(string column, NpgsqlDbType expected)
        {
            //A string parameter is sent as text, which jsonb will not accept without a cast.
            var parameter = Prepare("[1,2]", column);

            Assert.Equal(expected, parameter.NpgsqlDbType);
        }

        [Fact]
        public void A_json_column_with_a_length_is_a_varchar_and_stays_text()
        {
            var parameter = Prepare("[1,2]", "Capped");

            Assert.NotEqual(NpgsqlDbType.Jsonb, parameter.NpgsqlDbType);
            Assert.NotEqual(NpgsqlDbType.Json, parameter.NpgsqlDbType);
        }

        [Fact]
        public void A_plain_string_is_not_typed_as_json()
        {
            var parameter = Prepare("[1,2]", "Text");

            Assert.NotEqual(NpgsqlDbType.Jsonb, parameter.NpgsqlDbType);
            Assert.Equal("[1,2]", parameter.Value);
        }

        [Fact]
        public void A_parameter_that_is_not_an_Npgsql_one_is_still_made_UTC_and_cannot_be_typed_as_json()
        {
            //The UTC and kind adjustments need only the value; the JSON type needs an NpgsqlParameter.
            var moment = new OtherParameter { Value = new DateTimeOffset(2026, 11, 24, 13, 0, 0, TimeSpan.FromHours(5)) };
            var json = new OtherParameter { Value = "[1,2]" };

            Builder().PrepareParameter(moment, Column("Moment"));
            Builder().PrepareParameter(json, Column("Plain"));

            Assert.Equal(TimeSpan.Zero, Assert.IsType<DateTimeOffset>(moment.Value).Offset);
            Assert.Equal("[1,2]", json.Value);
        }

        private sealed class OtherParameter : DbParameter
        {
            public override DbType DbType { get; set; }
            public override ParameterDirection Direction { get; set; }
            public override bool IsNullable { get; set; }

            [AllowNull]
            public override string ParameterName { get; set; } = string.Empty;

            [AllowNull]
            public override string SourceColumn { get; set; } = string.Empty;

            public override object? Value { get; set; }
            public override bool SourceColumnNullMapping { get; set; }
            public override int Size { get; set; }

            public override void ResetDbType() { }
        }

        [Fact]
        public void A_null_value_is_left_alone()
        {
            var parameter = Prepare(DBNull.Value, "Moment");

            Assert.Equal(DBNull.Value, parameter.Value);
        }
    }
}

using System.Data;
using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Helpers;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     The generated reader is compiled once per entity type and cached by type only, so it has
    ///     to read what every provider returns. Npgsql hands back a <c>short</c> for a <c>byte</c>
    ///     property and a <c>DateTime</c> for a <c>DateTimeOffset</c> one; MySqlConnector hands back
    ///     a <c>DateTime</c> for a <c>DateTimeOffset</c>. A <see cref="DataTableReader"/> holding
    ///     those shapes stands in for the drivers, so no server is needed.
    /// </summary>
    public class ReaderPortabilityTests
    {
        private static TableDescriptor Describe(Type type)
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            var formatter = CommandBuilderFactory.GetCommandBuilder(new SqlConnection()).DatabaseFormatter;
            return TableDescriptorFactory.Create(type, formatter);
        }

        private static DataTable Table(params (string Name, Type Type)[] columns)
        {
            var table = new DataTable();
            foreach (var (name, type) in columns)
                table.Columns.Add(name, type);
            return table;
        }

        //byte

        [Fact]
        public async Task A_required_byte_is_read_from_a_short()
        {
            var table = Table(("Id", typeof(string)), ("Level", typeof(short)));
            table.Rows.Add("a", (short)200);

            var rows = await table.CreateDataReader().Read<PortableRequiredByteRow>(Describe(typeof(PortableRequiredByteRow)));

            Assert.Equal((byte)200, Assert.Single(rows).Level);
        }

        [Fact]
        public async Task A_required_byte_is_still_read_from_a_byte()
        {
            var table = Table(("Id", typeof(string)), ("Level", typeof(byte)));
            table.Rows.Add("a", (byte)200);

            var rows = await table.CreateDataReader().Read<PortableRequiredByteRow>(Describe(typeof(PortableRequiredByteRow)));

            Assert.Equal((byte)200, Assert.Single(rows).Level);
        }

        [Fact]
        public async Task An_optional_byte_is_read_from_a_short_and_a_null_is_the_default()
        {
            var table = Table(("Id", typeof(string)), ("Level", typeof(short)), ("MaybeLevel", typeof(short)));
            table.Rows.Add("full", (short)7, (short)255);
            table.Rows.Add("empty", DBNull.Value, DBNull.Value);

            var rows = await table.CreateDataReader().Read<PortableOptionalByteRow>(Describe(typeof(PortableOptionalByteRow)));

            Assert.Equal((byte)7, rows[0].Level);
            Assert.Equal((byte?)255, rows[0].MaybeLevel);
            Assert.Equal((byte)0, rows[1].Level);
            Assert.Null(rows[1].MaybeLevel);
        }

        [Fact]
        public async Task An_optional_byte_is_still_read_from_a_byte()
        {
            var table = Table(("Id", typeof(string)), ("Level", typeof(byte)), ("MaybeLevel", typeof(byte)));
            table.Rows.Add("full", (byte)7, (byte)255);
            table.Rows.Add("empty", DBNull.Value, DBNull.Value);

            var rows = await table.CreateDataReader().Read<PortableOptionalByteRow>(Describe(typeof(PortableOptionalByteRow)));

            Assert.Equal((byte)7, rows[0].Level);
            Assert.Equal((byte?)255, rows[0].MaybeLevel);
            Assert.Null(rows[1].MaybeLevel);
        }

        [Fact]
        public async Task A_byte_that_does_not_fit_throws_instead_of_being_truncated()
        {
            var table = Table(("Id", typeof(string)), ("Level", typeof(short)));
            table.Rows.Add("a", (short)300);

            await Assert.ThrowsAsync<OverflowException>(
                () => table.CreateDataReader().Read<PortableRequiredByteRow>(Describe(typeof(PortableRequiredByteRow))));
        }

        [Fact]
        public async Task An_optional_byte_that_does_not_fit_throws_instead_of_being_truncated()
        {
            var table = Table(("Id", typeof(string)), ("Level", typeof(short)), ("MaybeLevel", typeof(short)));
            table.Rows.Add("a", (short)1, (short)300);

            await Assert.ThrowsAsync<OverflowException>(
                () => table.CreateDataReader().Read<PortableOptionalByteRow>(Describe(typeof(PortableOptionalByteRow))));
        }

        //DateTimeOffset

        [Fact]
        public async Task A_required_DateTimeOffset_is_read_from_a_DateTime_as_UTC()
        {
            var table = Table(("Id", typeof(string)), ("Moment", typeof(DateTime)));
            table.Rows.Add("unspecified", new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Unspecified));
            table.Rows.Add("utc", new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc));

            var rows = await table.CreateDataReader().Read<PortableRequiredMomentRow>(Describe(typeof(PortableRequiredMomentRow)));

            var expected = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero);
            Assert.All(rows, r => Assert.Equal(expected, r.Moment));
            Assert.All(rows, r => Assert.Equal(TimeSpan.Zero, r.Moment.Offset));
        }

        [Fact]
        public async Task A_Local_DateTime_is_converted_to_the_same_instant()
        {
            var local = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Local);
            var table = Table(("Id", typeof(string)), ("Moment", typeof(DateTime)));
            table.Rows.Add("a", local);

            var rows = await table.CreateDataReader().Read<PortableRequiredMomentRow>(Describe(typeof(PortableRequiredMomentRow)));

            Assert.Equal(local.ToUniversalTime(), Assert.Single(rows).Moment.UtcDateTime);
        }

        [Fact]
        public async Task A_required_DateTimeOffset_is_still_read_from_a_DateTimeOffset()
        {
            var moment = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.FromHours(2));
            var table = Table(("Id", typeof(string)), ("Moment", typeof(DateTimeOffset)));
            table.Rows.Add("a", moment);

            var rows = await table.CreateDataReader().Read<PortableRequiredMomentRow>(Describe(typeof(PortableRequiredMomentRow)));

            var row = Assert.Single(rows);
            Assert.Equal(moment, row.Moment);
            Assert.Equal(TimeSpan.FromHours(2), row.Moment.Offset);
        }

        [Fact]
        public async Task An_optional_DateTimeOffset_is_read_from_a_DateTime_and_a_null_is_the_default()
        {
            var table = Table(("Id", typeof(string)), ("Moment", typeof(DateTime)), ("MaybeMoment", typeof(DateTime)));
            table.Rows.Add("utc", new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc), new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc));
            table.Rows.Add("unspecified", new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Unspecified), new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Unspecified));
            table.Rows.Add("empty", DBNull.Value, DBNull.Value);

            var rows = await table.CreateDataReader().Read<PortableOptionalMomentRow>(Describe(typeof(PortableOptionalMomentRow)));

            var expected = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero);
            Assert.Equal(expected, rows[0].Moment);
            Assert.Equal(expected, rows[0].MaybeMoment);
            Assert.Equal(expected, rows[1].Moment);
            Assert.Equal(expected, rows[1].MaybeMoment);
            Assert.Equal(default, rows[2].Moment);
            Assert.Null(rows[2].MaybeMoment);
        }

        [Fact]
        public async Task An_optional_DateTimeOffset_is_still_read_from_a_DateTimeOffset()
        {
            var moment = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.FromHours(-5));
            var table = Table(("Id", typeof(string)), ("Moment", typeof(DateTimeOffset)), ("MaybeMoment", typeof(DateTimeOffset)));
            table.Rows.Add("a", moment, moment);

            var rows = await table.CreateDataReader().Read<PortableOptionalMomentRow>(Describe(typeof(PortableOptionalMomentRow)));

            var row = Assert.Single(rows);
            Assert.Equal(moment, row.Moment);
            Assert.Equal(TimeSpan.FromHours(-5), row.Moment.Offset);
            Assert.Equal(moment, row.MaybeMoment);
        }

        [Fact]
        public async Task ReadSingle_reads_the_other_drivers_shapes_too()
        {
            var table = Table(("Id", typeof(string)), ("Level", typeof(short)), ("MaybeLevel", typeof(short)), ("Moment", typeof(DateTime)), ("MaybeMoment", typeof(DateTime)));
            table.Rows.Add("a", (short)3, (short)4, new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc));

            var row = await table.CreateDataReader().ReadSingle<PortableMixedRow>(Describe(typeof(PortableMixedRow)));

            Assert.NotNull(row);
            Assert.Equal((byte)3, row.Level);
            Assert.Equal((byte?)4, row.MaybeLevel);
            Assert.Equal(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), row.Moment);
            Assert.Equal(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), row.MaybeMoment);
        }
    }

    [Table("PortableRequiredByte")]
    public sealed class PortableRequiredByteRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Level", Required = true)]
        public byte Level { get; set; }
    }

    [Table("PortableOptionalByte")]
    public sealed class PortableOptionalByteRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Level")]
        public byte Level { get; set; }

        [Column(Name = "MaybeLevel")]
        public byte? MaybeLevel { get; set; }
    }

    [Table("PortableRequiredMoment")]
    public sealed class PortableRequiredMomentRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Moment", Required = true)]
        public DateTimeOffset Moment { get; set; }
    }

    [Table("PortableOptionalMoment")]
    public sealed class PortableOptionalMomentRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Moment")]
        public DateTimeOffset Moment { get; set; }

        [Column(Name = "MaybeMoment")]
        public DateTimeOffset? MaybeMoment { get; set; }
    }

    [Table("PortableMixed")]
    public sealed class PortableMixedRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Level")]
        public byte Level { get; set; }

        [Column(Name = "MaybeLevel")]
        public byte? MaybeLevel { get; set; }

        [Column(Name = "Moment")]
        public DateTimeOffset Moment { get; set; }

        [Column(Name = "MaybeMoment")]
        public DateTimeOffset? MaybeMoment { get; set; }
    }
}

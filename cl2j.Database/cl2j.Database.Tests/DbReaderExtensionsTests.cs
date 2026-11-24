using System.Data;
using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Exceptions;
using cl2j.Database.Helpers;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     The read path compiles a reader per entity type and caches it for the lifetime of the
    ///     process. A <see cref="DataTableReader"/> is a <see cref="System.Data.Common.DbDataReader"/>,
    ///     so the generated code can be exercised without a server. What it cannot reproduce is
    ///     provider behaviour (SqlClient value types, DBNull on a typed getter); that stays with
    ///     the integration tests.
    ///
    ///     Every scenario uses its own entity type, because the compiled reader is cached by type.
    /// </summary>
    public class DbReaderExtensionsTests
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

        [Fact]
        public async Task Required_columns_are_read_through_their_typed_getters()
        {
            var table = Table(
                ("Id", typeof(string)), ("Flag", typeof(bool)), ("Small", typeof(short)), ("Count", typeof(int)),
                ("Big", typeof(long)), ("Price", typeof(decimal)), ("Rate", typeof(float)), ("Ratio", typeof(double)),
                ("Ref", typeof(Guid)), ("Stamp", typeof(DateTime)), ("Kind", typeof(int)), ("Moment", typeof(DateTimeOffset)),
                ("Payload", typeof(byte[])), ("Elapsed", typeof(TimeSpan)));
            var guid = Guid.NewGuid();
            var stamp = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
            var moment = new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.FromHours(2));
            table.Rows.Add("a", true, (short)7, 42, 9_000_000_000L, 12.34m, 1.5f, 2.25d, guid, stamp, 1, moment, new byte[] { 1, 2 }, TimeSpan.FromMinutes(3));

            var rows = await table.CreateDataReader().Read<RequiredScalarsRow>(Describe(typeof(RequiredScalarsRow)));

            var row = Assert.Single(rows);
            Assert.Equal("a", row.Id);
            Assert.True(row.Flag);
            Assert.Equal((short)7, row.Small);
            Assert.Equal(42, row.Count);
            Assert.Equal(9_000_000_000L, row.Big);
            Assert.Equal(12.34m, row.Price);
            Assert.Equal(1.5f, row.Rate);
            Assert.Equal(2.25d, row.Ratio);
            Assert.Equal(guid, row.Ref);
            Assert.Equal(stamp, row.Stamp);
            Assert.Equal(SampleKind.Second, row.Kind);
            Assert.Equal(moment, row.Moment);
            Assert.Equal(new byte[] { 1, 2 }, row.Payload);
            Assert.Equal(TimeSpan.FromMinutes(3), row.Elapsed);
        }

        [Fact]
        public async Task Read_returns_every_row_in_reader_order()
        {
            var table = Table(("Id", typeof(string)), ("Count", typeof(int)));
            table.Rows.Add("a", 1);
            table.Rows.Add("b", 2);
            table.Rows.Add("c", 3);

            var rows = await table.CreateDataReader().Read<TwoColumnRow>(Describe(typeof(TwoColumnRow)));

            Assert.Equal(["a", "b", "c"], rows.Select(r => r.Id));
            Assert.Equal([1, 2, 3], rows.Select(r => r.Count));
        }

        [Fact]
        public async Task Read_closes_the_reader()
        {
            var table = Table(("Id", typeof(string)), ("Count", typeof(int)));
            table.Rows.Add("a", 1);
            var reader = table.CreateDataReader();

            await reader.Read<ClosedAfterReadRow>(Describe(typeof(ClosedAfterReadRow)));

            Assert.True(reader.IsClosed);
        }

        [Fact]
        public async Task Read_of_an_empty_reader_returns_an_empty_list()
        {
            var table = Table(("Id", typeof(string)), ("Count", typeof(int)));

            var rows = await table.CreateDataReader().Read<EmptyReadRow>(Describe(typeof(EmptyReadRow)));

            Assert.Empty(rows);
        }

        [Fact]
        public async Task Columns_are_matched_by_name_whatever_their_order_in_the_result_set()
        {
            //Issue #25: two columns of the same shape used to swap silently when the SELECT listed
            //them in another order than the type declares its properties.
            var table = Table(("Second", typeof(string)), ("Id", typeof(string)), ("First", typeof(string)));
            table.Rows.Add("two", "k", "one");

            var rows = await table.CreateDataReader().Read<ReorderedRow>(Describe(typeof(ReorderedRow)));

            var row = Assert.Single(rows);
            Assert.Equal("k", row.Id);
            Assert.Equal("one", row.First);
            Assert.Equal("two", row.Second);
        }

        [Fact]
        public async Task Columns_the_type_does_not_map_are_ignored()
        {
            var table = Table(("Extra", typeof(int)), ("Id", typeof(string)), ("Count", typeof(int)));
            table.Rows.Add(99, "a", 1);

            var rows = await table.CreateDataReader().Read<ExtraColumnRow>(Describe(typeof(ExtraColumnRow)));

            var row = Assert.Single(rows);
            Assert.Equal("a", row.Id);
            Assert.Equal(1, row.Count);
        }

        [Fact]
        public async Task A_missing_column_fails_with_the_real_exception_not_the_reflection_wrapper()
        {
            var table = Table(("Id", typeof(string)));
            table.Rows.Add("a");

            var ex = await Assert.ThrowsAnyAsync<Exception>(
                () => table.CreateDataReader().Read<MissingColumnRow>(Describe(typeof(MissingColumnRow))));

            Assert.IsNotType<System.Reflection.TargetInvocationException>(ex);
            Assert.Contains("Count", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_missing_column_closes_the_reader()
        {
            var table = Table(("Id", typeof(string)));
            table.Rows.Add("a");
            var reader = table.CreateDataReader();

            await Assert.ThrowsAnyAsync<Exception>(() => reader.Read<MissingColumnClosesRow>(Describe(typeof(MissingColumnClosesRow))));

            Assert.True(reader.IsClosed);
        }

        [Fact]
        public async Task Read_refuses_a_type_that_is_not_public()
        {
            var table = Table(("Id", typeof(string)));
            table.Rows.Add("a");

            var ex = await Assert.ThrowsAsync<DatabaseException>(
                () => table.CreateDataReader().Read<InternalRow>(Describe(typeof(InternalRow))));

            Assert.Contains("must be public", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Optional_columns_map_DBNull_to_the_default_and_a_value_to_itself()
        {
            var table = Table(
                ("Id", typeof(string)), ("Name", typeof(string)), ("Count", typeof(int)), ("MaybeCount", typeof(int)),
                ("MaybeRef", typeof(Guid)), ("MaybeWhen", typeof(DateTime)), ("MaybeMoment", typeof(DateTimeOffset)),
                ("MaybeFlag", typeof(bool)), ("Kind", typeof(int)), ("Payload", typeof(byte[])));
            var guid = Guid.NewGuid();
            var when = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            var moment = new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero);
            table.Rows.Add("full", "n", 5, 6, guid, when, moment, true, 1, new byte[] { 9 });
            table.Rows.Add("empty", DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 0, DBNull.Value);

            var rows = await table.CreateDataReader().Read<OptionalRow>(Describe(typeof(OptionalRow)));

            var full = rows[0];
            Assert.Equal("n", full.Name);
            Assert.Equal(5, full.Count);
            Assert.Equal(6, full.MaybeCount);
            Assert.Equal(guid, full.MaybeRef);
            Assert.Equal(when, full.MaybeWhen);
            Assert.Equal(moment, full.MaybeMoment);
            Assert.True(full.MaybeFlag);
            Assert.Equal(SampleKind.Second, full.Kind);
            Assert.Equal(new byte[] { 9 }, full.Payload);

            var empty = rows[1];
            Assert.Null(empty.Name);
            Assert.Equal(0, empty.Count);
            Assert.Null(empty.MaybeCount);
            Assert.Null(empty.MaybeRef);
            Assert.Null(empty.MaybeWhen);
            Assert.Null(empty.MaybeMoment);
            Assert.Null(empty.MaybeFlag);
            Assert.Equal(SampleKind.First, empty.Kind);
            Assert.Null(empty.Payload);
        }

        [Fact]
        public async Task A_nullable_enum_column_is_read_as_the_enum()
        {
            var table = Table(("Id", typeof(string)), ("MaybeKind", typeof(int)));
            table.Rows.Add("a", 1);
            table.Rows.Add("b", DBNull.Value);

            var rows = await table.CreateDataReader().Read<NullableEnumRow>(Describe(typeof(NullableEnumRow)));

            Assert.Equal(SampleKind.Second, rows[0].MaybeKind);
            Assert.Null(rows[1].MaybeKind);
        }

        [Fact]
        public async Task Json_columns_are_deserialized_into_the_property_type()
        {
            var table = Table(("Id", typeof(string)), ("Details", typeof(string)), ("Tags", typeof(string)));
            table.Rows.Add("a", "{\"Label\":\"hello\",\"Size\":3}", "[\"x\",\"y\"]");

            var rows = await table.CreateDataReader().Read<JsonRow>(Describe(typeof(JsonRow)));

            var row = Assert.Single(rows);
            Assert.Equal("hello", row.Details!.Label);
            Assert.Equal(3, row.Details.Size);
            Assert.Equal(["x", "y"], row.Tags!);
        }

        [Fact]
        public async Task ReadSingle_of_an_empty_reader_returns_null()
        {
            var table = Table(("Id", typeof(string)), ("Count", typeof(int)));

            var row = await table.CreateDataReader().ReadSingle<SingleEmptyRow>(Describe(typeof(SingleEmptyRow)));

            Assert.Null(row);
        }

        [Fact]
        public async Task ReadSingle_returns_the_row_and_closes_the_reader()
        {
            var table = Table(("Id", typeof(string)), ("Count", typeof(int)));
            table.Rows.Add("a", 1);
            var reader = table.CreateDataReader();

            var row = await reader.ReadSingle<SingleRow>(Describe(typeof(SingleRow)));

            Assert.NotNull(row);
            Assert.Equal("a", row.Id);
            Assert.Equal(1, row.Count);
            Assert.True(reader.IsClosed);
        }

        [Fact]
        public async Task ReadSingle_takes_the_first_row_when_there_are_several()
        {
            var table = Table(("Id", typeof(string)), ("Count", typeof(int)));
            table.Rows.Add("first", 1);
            table.Rows.Add("second", 2);

            var row = await table.CreateDataReader().ReadSingle<SingleOfManyRow>(Describe(typeof(SingleOfManyRow)));

            Assert.Equal("first", row!.Id);
        }

        [Fact]
        public async Task ReadSingle_matches_columns_by_name()
        {
            var table = Table(("Count", typeof(int)), ("Id", typeof(string)));
            table.Rows.Add(4, "a");

            var row = await table.CreateDataReader().ReadSingle<SingleNamedRow>(Describe(typeof(SingleNamedRow)));

            Assert.Equal("a", row!.Id);
            Assert.Equal(4, row.Count);
        }

        [Fact]
        public async Task ReadSingle_with_a_missing_column_fails_with_the_real_exception_and_closes_the_reader()
        {
            var table = Table(("Id", typeof(string)));
            table.Rows.Add("a");
            var reader = table.CreateDataReader();

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => reader.ReadSingle<SingleMissingRow>(Describe(typeof(SingleMissingRow))));

            Assert.IsNotType<System.Reflection.TargetInvocationException>(ex);
            Assert.True(reader.IsClosed);
        }
    }

    [Table("TwoColumn")]
    public sealed class TwoColumnRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Count", Required = true)]
        public int Count { get; set; }
    }

    [Table("RequiredScalars")]
    public sealed class RequiredScalarsRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Flag", Required = true)]
        public bool Flag { get; set; }

        [Column(Name = "Small", Required = true)]
        public short Small { get; set; }

        [Column(Name = "Count", Required = true)]
        public int Count { get; set; }

        [Column(Name = "Big", Required = true)]
        public long Big { get; set; }

        [Column(Name = "Price", Required = true, Length = 18, Decimals = 2)]
        public decimal Price { get; set; }

        [Column(Name = "Rate", Required = true)]
        public float Rate { get; set; }

        [Column(Name = "Ratio", Required = true)]
        public double Ratio { get; set; }

        [Column(Name = "Ref", Required = true)]
        public Guid Ref { get; set; }

        [Column(Name = "Stamp", Required = true)]
        public DateTime Stamp { get; set; }

        [Column(Name = "Kind", Required = true)]
        public SampleKind Kind { get; set; }

        [Column(Name = "Moment", Required = true)]
        public DateTimeOffset Moment { get; set; }

        [Column(Name = "Payload", Required = true)]
        public byte[] Payload { get; set; } = [];

        [Column(Name = "Elapsed", Required = true)]
        public TimeSpan Elapsed { get; set; }
    }

    [Table("ClosedAfterRead")]
    public sealed class ClosedAfterReadRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Count", Required = true)]
        public int Count { get; set; }
    }

    [Table("EmptyRead")]
    public sealed class EmptyReadRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Count", Required = true)]
        public int Count { get; set; }
    }

    [Table("Reordered")]
    public sealed class ReorderedRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "First", Required = true)]
        public string First { get; set; } = string.Empty;

        [Column(Name = "Second", Required = true)]
        public string Second { get; set; } = string.Empty;
    }

    [Table("ExtraColumn")]
    public sealed class ExtraColumnRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Count", Required = true)]
        public int Count { get; set; }
    }

    [Table("MissingColumn")]
    public sealed class MissingColumnRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Count", Required = true)]
        public int Count { get; set; }
    }

    [Table("MissingColumnCloses")]
    public sealed class MissingColumnClosesRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Count", Required = true)]
        public int Count { get; set; }
    }

    [Table("Internal")]
    internal sealed class InternalRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;
    }

    [Table("Optional")]
    public sealed class OptionalRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Name", Length = 50)]
        public string? Name { get; set; }

        [Column(Name = "Count")]
        public int Count { get; set; }

        [Column(Name = "MaybeCount")]
        public int? MaybeCount { get; set; }

        [Column(Name = "MaybeRef")]
        public Guid? MaybeRef { get; set; }

        [Column(Name = "MaybeWhen")]
        public DateTime? MaybeWhen { get; set; }

        [Column(Name = "MaybeMoment")]
        public DateTimeOffset? MaybeMoment { get; set; }

        [Column(Name = "MaybeFlag")]
        public bool? MaybeFlag { get; set; }

        [Column(Name = "Kind")]
        public SampleKind Kind { get; set; }

        [Column(Name = "Payload")]
        public byte[]? Payload { get; set; }
    }

    [Table("NullableEnum")]
    public sealed class NullableEnumRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "MaybeKind")]
        public SampleKind? MaybeKind { get; set; }
    }

    public sealed class JsonDetails
    {
        public string Label { get; set; } = string.Empty;

        public int Size { get; set; }
    }

    [Table("JsonDoc")]
    public sealed class JsonRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Details", Json = true)]
        public JsonDetails? Details { get; set; }

        [Column(Name = "Tags", Json = true)]
        public List<string>? Tags { get; set; }
    }

    [Table("SingleEmpty")]
    public sealed class SingleEmptyRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Count", Required = true)]
        public int Count { get; set; }
    }

    [Table("Single")]
    public sealed class SingleRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Count", Required = true)]
        public int Count { get; set; }
    }

    [Table("SingleOfMany")]
    public sealed class SingleOfManyRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Count", Required = true)]
        public int Count { get; set; }
    }

    [Table("SingleNamed")]
    public sealed class SingleNamedRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Count", Required = true)]
        public int Count { get; set; }
    }

    [Table("SingleMissing")]
    public sealed class SingleMissingRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Count", Required = true)]
        public int Count { get; set; }
    }
}

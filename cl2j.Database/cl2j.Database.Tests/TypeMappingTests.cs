using System.Data;
using System.Globalization;
using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Helpers;
using cl2j.Database.TypeMatrix;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     Every type the README says it maps to SQL Server, checked on the three paths that need
    ///     no server: the column type the DDL declares, the parameter a value is bound as, and the
    ///     generated reader fed from a <see cref="DataTable"/>. The <c>Mx*</c> entities carry each
    ///     type three times: plain (<c>V</c>), required (<c>R</c>) and nullable (<c>N</c>), which
    ///     are the three branches of the generated reader.
    ///
    ///     What a real SqlClient reader hands back, and what a real server accepts, is the job of
    ///     <c>TypeMatrixTests</c> in the integration project.
    /// </summary>
    public class TypeMappingTests
    {
        private static IDatabaseFormatter Formatter()
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            return CommandBuilderFactory.GetCommandBuilder(new SqlConnection()).DatabaseFormatter;
        }

        private static TableDescriptor Describe(Type type) => TableDescriptorFactory.Create(type, Formatter());

        private static ColumnDescriptor Column(Type type, string name) => Describe(type).Columns.Single(c => c.Name == name);

        //The type each column is declared as, and the type SqlClient hands back for it.
        public static TheoryData<Type, string> Declarations => new()
        {
            { typeof(MxBool), "bit" },
            { typeof(MxByte), "tinyint" },
            { typeof(MxShort), "smallint" },
            { typeof(MxInt), "int" },
            { typeof(MxLong), "bigint" },
            { typeof(MxFloat), "real" },
            { typeof(MxDouble), "float" },
            { typeof(MxDecimal), "decimal(18,2)" },
            { typeof(MxGuid), "uniqueidentifier" },
            { typeof(MxString), "varchar(100)" },
            { typeof(MxBytes), "varbinary(max)" },
            { typeof(MxDateTime), "datetime2" },
            { typeof(MxDateTimeOffset), "datetimeoffset" },
            { typeof(MxDateOnly), "date" },
            { typeof(MxTimeOnly), "time" },
            { typeof(MxTimeSpan), "time" },
            { typeof(MxChar), "char(1)" },
            { typeof(MxKind), "int" },
        };

        [Theory]
        [MemberData(nameof(Declarations))]
        public void A_type_is_declared_the_same_plain_required_and_nullable(Type row, string expected)
        {
            var formatter = Formatter();

            Assert.Equal(expected, formatter.GetColumnDataType(Column(row, "V")));
            Assert.Equal(expected, formatter.GetColumnDataType(Column(row, "R")));
            Assert.Equal(expected, formatter.GetColumnDataType(Column(row, "N")));
        }

        [Fact]
        public void TypeName_wins_over_the_mapping()
        {
            Assert.Equal("nvarchar(50)", Formatter().GetColumnDataType(Column(typeof(OverriddenRow), "Name")));
            Assert.Equal("datetime", Formatter().GetColumnDataType(Column(typeof(OverriddenRow), "Legacy")));
        }

        [Fact]
        public void A_json_column_is_nvarchar_and_a_length_bounds_it()
        {
            Assert.Equal("nvarchar(max)", Formatter().GetColumnDataType(Column(typeof(OverriddenRow), "Body")));
            Assert.Equal("nvarchar(200)", Formatter().GetColumnDataType(Column(typeof(OverriddenRow), "BoundedBody")));
        }

        [Fact]
        public void A_byte_array_with_a_length_is_a_bounded_varbinary()
        {
            Assert.Equal("varbinary(16)", Formatter().GetColumnDataType(Column(typeof(OverriddenRow), "Hash")));
        }

        //What a value of each type is bound as. The key is the SqlClient type, which is what the
        //server sees.
        public static TheoryData<Type, object, SqlDbType> Bindings => new()
        {
            { typeof(MxBool), true, SqlDbType.Bit },
            { typeof(MxByte), (byte)200, SqlDbType.TinyInt },
            { typeof(MxShort), (short)-5, SqlDbType.SmallInt },
            { typeof(MxInt), 42, SqlDbType.Int },
            { typeof(MxLong), 5_000_000_000L, SqlDbType.BigInt },
            { typeof(MxFloat), 1.5f, SqlDbType.Real },
            { typeof(MxDouble), 2.25d, SqlDbType.Float },
            { typeof(MxDecimal), 12.34m, SqlDbType.Decimal },
            { typeof(MxGuid), Guid.NewGuid(), SqlDbType.UniqueIdentifier },
            { typeof(MxString), "text", SqlDbType.NVarChar },
            { typeof(MxBytes), new byte[] { 1 }, SqlDbType.VarBinary },
            { typeof(MxDateTime), new DateTime(2026, 9, 27), SqlDbType.DateTime2 },
            { typeof(MxDateTimeOffset), DateTimeOffset.UtcNow, SqlDbType.DateTimeOffset },
            { typeof(MxDateOnly), new DateOnly(2026, 9, 27), SqlDbType.Date },
            { typeof(MxTimeOnly), new TimeOnly(13, 14), SqlDbType.Time },
            { typeof(MxTimeSpan), TimeSpan.FromHours(1), SqlDbType.Time },
            { typeof(MxChar), 'x', SqlDbType.NVarChar },
            { typeof(MxKind), MatrixKind.One, SqlDbType.Int },
        };

        private static SqlParameter Bind(Type row, string column, object? value)
            => (SqlParameter)ParameterDeclaration.Bind(new SqlCommand(), column, Column(row, column), value);

        [Theory]
        [MemberData(nameof(Bindings))]
        public void A_value_is_bound_as_the_type_of_its_column(Type row, object value, SqlDbType expected)
        {
            Assert.Equal(expected, Bind(row, "V", value).SqlDbType);
            Assert.Equal(expected, Bind(row, "R", value).SqlDbType);
            Assert.Equal(expected, Bind(row, "N", value).SqlDbType);
        }

        [Theory]
        [MemberData(nameof(Bindings))]
        public void A_null_is_bound_as_the_type_of_its_column_and_sent_as_a_database_null(Type row, object value, SqlDbType expected)
        {
            Assert.NotNull(value);

            var parameter = Bind(row, "N", null);

            Assert.Equal(expected, parameter.SqlDbType);
            Assert.Equal(DBNull.Value, parameter.Value);
        }

        [Fact]
        public void A_DateTime_is_bound_as_datetime2_so_year_one_and_every_tick_survive()
        {
            //Left to SqlClient it is a datetime: nothing before 1753 and 1/300 s of precision.
            var parameter = Bind(typeof(MxDateTime), "V", DateTime.MinValue);

            Assert.Equal(SqlDbType.DateTime2, parameter.SqlDbType);
            Assert.Equal(DateTime.MinValue, parameter.Value);
        }

        [Fact]
        public void A_DateTime_for_a_column_declared_datetime_is_left_to_the_provider()
        {
            Assert.Equal(SqlDbType.DateTime, Bind(typeof(OverriddenRow), "Legacy", new DateTime(2026, 9, 27)).SqlDbType);
            Assert.Equal(SqlDbType.DateTime, Bind(typeof(OverriddenRow), "Legacy", null).SqlDbType);
        }

        [Fact]
        public void A_DateTime_in_an_anonymous_parameter_is_bound_as_datetime2()
        {
            var column = Describe(new { When = DateTime.UtcNow }.GetType()).Columns.Single();

            var parameter = (SqlParameter)ParameterDeclaration.Bind(new SqlCommand(), "When", column, DateTime.UtcNow);

            Assert.Equal(SqlDbType.DateTime2, parameter.SqlDbType);
        }

        [Theory]
        [InlineData("9999999999.123456789012345678")]
        [InlineData("-9999999999.5")]
        [InlineData("0.01")]
        public void A_decimal_of_up_to_ten_integer_digits_takes_the_fixed_declaration(string text)
        {
            var parameter = Bind(typeof(MxDecimal), "V", decimal.Parse(text, CultureInfo.InvariantCulture));

            Assert.Equal(((byte)38, (byte)18), (parameter.Precision, parameter.Scale));
        }

        [Theory]
        [InlineData("10000000000")]
        [InlineData("-9999999999999999.99")]
        [InlineData("99999999999999999999.99")]
        public void A_decimal_of_eleven_or_more_integer_digits_is_left_to_the_provider(string text)
        {
            //Scaled to 18 decimals in a System.Decimal, eleven integer digits no longer fit its
            //96 bits: SqlClient threw OverflowException on the insert of any such value.
            var value = decimal.Parse(text, CultureInfo.InvariantCulture);

            var parameter = Bind(typeof(MxDecimal), "V", value);

            Assert.Equal(((byte)0, (byte)0), (parameter.Precision, parameter.Scale));
            Assert.Equal(value, parameter.Value);
        }

        //The bulk insert path: the DataTable is what SqlBulkCopy writes.
        [Theory]
        [MemberData(nameof(Bindings))]
        public void A_bulk_insert_table_declares_each_column_with_the_type_it_wraps(Type row, object value, SqlDbType bound)
        {
            Assert.NotNull(value);
            Assert.NotEqual((SqlDbType)(-1), bound);
            var columns = Describe(row).Columns.Where(c => c.Name != "Id").ToList();

            var table = DataTableHelpers.CreateDataTable(Array.Empty<object>(), "T", columns);

            Assert.All(columns, c => Assert.Equal(Nullable.GetUnderlyingType(c.Property.PropertyType) ?? c.Property.PropertyType, table.Columns[c.Name]!.DataType));
        }

        [Fact]
        public void A_DateTimeOffset_keeps_its_value_and_its_offset_in_a_bulk_insert_table()
        {
            //The table used to carry the current time instead of the value, for every
            //DateTimeOffset property without a column default.
            var moment = new DateTimeOffset(2026, 9, 27, 13, 14, 15, TimeSpan.FromMinutes(330));
            var row = new MxDateTimeOffset { V = moment, R = moment.AddDays(1), N = moment.AddDays(2) };

            var table = DataTableHelpers.CreateDataTable([row], "T", Describe(typeof(MxDateTimeOffset)).Columns.Where(c => c.Name != "Id"));

            var cells = table.Rows[0];
            Assert.Equal(moment, (DateTimeOffset)cells["V"]);
            Assert.Equal(TimeSpan.FromMinutes(330), ((DateTimeOffset)cells["V"]).Offset);
            Assert.Equal(moment.AddDays(1), (DateTimeOffset)cells["R"]);
            Assert.Equal(moment.AddDays(2), (DateTimeOffset)cells["N"]);
        }

        [Fact]
        public void A_null_nullable_DateTimeOffset_is_a_database_null_in_a_bulk_insert_table()
        {
            //Insert writes NULL for it, because the property's type is Nullable, not DateTimeOffset.
            var row = new MxDateTimeOffset { V = DateTimeOffset.UnixEpoch, R = DateTimeOffset.UnixEpoch, N = null };

            var table = DataTableHelpers.CreateDataTable([row], "T", Describe(typeof(MxDateTimeOffset)).Columns.Where(c => c.Name != "Id"));

            Assert.Equal(DBNull.Value, table.Rows[0]["N"]);
        }

        //The generated reader. The provider type is what SqlClient returns for the column; the
        //property value is what must come out.
        private sealed record ReadCase(Type Row, Type Provider, object V, object R, object N, object? EV = null, object? ER = null, object? EN = null);

        private static readonly ReadCase[] ReadCases =
        [
            new(typeof(MxBool), typeof(bool), true, true, true),
            new(typeof(MxByte), typeof(byte), (byte)200, (byte)255, (byte)7),
            new(typeof(MxShort), typeof(short), (short)-5, short.MaxValue, short.MinValue),
            new(typeof(MxInt), typeof(int), 42, -7, int.MaxValue),
            new(typeof(MxLong), typeof(long), long.MaxValue, long.MinValue, 5_000_000_000L),
            new(typeof(MxFloat), typeof(float), 1.5f, 3.4028235E+38f, -0.25f),
            new(typeof(MxDouble), typeof(double), 2.25d, double.MaxValue, 0.1d),
            new(typeof(MxDecimal), typeof(decimal), 12.34m, -9999999999999999.99m, 0.01m),
            new(typeof(MxGuid), typeof(Guid), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            new(typeof(MxString), typeof(string), "hello", "café", "x"),
            new(typeof(MxBytes), typeof(byte[]), new byte[] { 1, 2, 3 }, new byte[] { 0, 255 }, new byte[] { 9 }),
            new(typeof(MxDateTime), typeof(DateTime), new DateTime(2026, 9, 27, 13, 14, 15), DateTime.MinValue, DateTime.MaxValue),
            new(typeof(MxDateTimeOffset), typeof(DateTimeOffset), new DateTimeOffset(2026, 9, 27, 13, 14, 15, TimeSpan.FromMinutes(330)), new DateTimeOffset(2020, 2, 29, 23, 59, 59, TimeSpan.FromHours(-8)), DateTimeOffset.UnixEpoch),
            //SqlClient returns a date as a DateTime, a time as a TimeSpan and a char(1) as a string.
            new(typeof(MxDateOnly), typeof(DateTime), new DateTime(2026, 9, 27), new DateTime(1, 1, 1), new DateTime(9999, 12, 31),
                new DateOnly(2026, 9, 27), DateOnly.MinValue, DateOnly.MaxValue),
            new(typeof(MxTimeOnly), typeof(TimeSpan), new TimeSpan(13, 14, 15), TimeSpan.Zero, TimeSpan.FromHours(23),
                new TimeOnly(13, 14, 15), TimeOnly.MinValue, new TimeOnly(23, 0)),
            new(typeof(MxTimeSpan), typeof(TimeSpan), new TimeSpan(13, 14, 15), TimeSpan.Zero, TimeSpan.FromHours(23)),
            new(typeof(MxChar), typeof(string), "Z", "a", "k", 'Z', 'a', 'k'),
            new(typeof(MxKind), typeof(int), 1, 1000, 0, MatrixKind.One, MatrixKind.Big, MatrixKind.Zero),
        ];

        public static TheoryData<string> ReadTypes
        {
            get
            {
                var data = new TheoryData<string>();
                foreach (var c in ReadCases)
                    data.Add(c.Row.Name);
                return data;
            }
        }

        private static DataTable TableFor(ReadCase c, object? v, object? r, object? n)
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("V", c.Provider);
            table.Columns.Add("R", c.Provider);
            table.Columns.Add("N", c.Provider);
            table.Rows.Add(1, v ?? DBNull.Value, r ?? DBNull.Value, n ?? DBNull.Value);
            return table;
        }

        private static object? Read(Type row, string property, object entity) => row.GetProperty(property)!.GetValue(entity);

        private static void AssertSame(object? expected, object? actual, string what)
        {
            if (expected is DateTimeOffset moment)
            {
                var other = Assert.IsType<DateTimeOffset>(actual);
                Assert.True(moment.Equals(other) && moment.Offset == other.Offset, $"{what}: {moment:o} expected, {other:o} read");
            }
            else
                Assert.True(Equals(expected, actual) || (expected is byte[] a && actual is byte[] b && a.SequenceEqual(b)), $"{what}: '{expected}' expected, '{actual}' read");
        }

        private static async Task<(object List, object Single)> ReadBoth(Type row, ReadCase c, object? v, object? r, object? n)
        {
            var descriptor = Describe(row);
            var readList = typeof(DbReaderExtensions).GetMethod(nameof(DbReaderExtensions.Read))!.MakeGenericMethod(row);
            var readSingle = typeof(DbReaderExtensions).GetMethod(nameof(DbReaderExtensions.ReadSingle))!.MakeGenericMethod(row);

            var list = await InvokeAsync(readList, TableFor(c, v, r, n).CreateDataReader(), descriptor);
            var single = await InvokeAsync(readSingle, TableFor(c, v, r, n).CreateDataReader(), descriptor);
            return (Assert.Single(((System.Collections.IEnumerable)list!).Cast<object>()), single!);
        }

        private static async Task<object?> InvokeAsync(System.Reflection.MethodInfo method, params object[] args)
        {
            var task = (Task)method.Invoke(null, args)!;
            await task;
            return task.GetType().GetProperty("Result")!.GetValue(task);
        }

        [Theory]
        [MemberData(nameof(ReadTypes))]
        public async Task The_generated_reader_gives_back_the_value_on_every_shape_of_column(string type)
        {
            var c = ReadCases.Single(x => x.Row.Name == type);

            //A list and a single row come from two generated readers.
            var (list, single) = await ReadBoth(c.Row, c, c.V, c.R, c.N);

            foreach (var entity in new[] { list, single })
            {
                AssertSame(c.EV ?? c.V, Read(c.Row, "V", entity), "V (plain)");
                AssertSame(c.ER ?? c.R, Read(c.Row, "R", entity), "R (required)");
                AssertSame(c.EN ?? c.N, Read(c.Row, "N", entity), "N (nullable)");
            }
        }

        [Theory]
        [MemberData(nameof(ReadTypes))]
        public async Task The_generated_reader_gives_back_null_for_a_database_null(string type)
        {
            var c = ReadCases.Single(x => x.Row.Name == type);

            var (list, single) = await ReadBoth(c.Row, c, null, c.R, null);

            foreach (var entity in new[] { list, single })
            {
                var propertyType = c.Row.GetProperty("V")!.PropertyType;
                AssertSame(propertyType.IsValueType ? Activator.CreateInstance(propertyType) : null, Read(c.Row, "V", entity), "V (plain, DBNull)");
                Assert.Null(Read(c.Row, "N", entity));
            }
        }
    }

    [Table("Overridden")]
    public sealed class OverriddenRow
    {
        [Column(Name = "Name", TypeName = "nvarchar(50)")]
        public string Name { get; set; } = string.Empty;

        [Column(Name = "Legacy", TypeName = "datetime")]
        public DateTime Legacy { get; set; }

        [Column(Name = "Body", Json = true)]
        public List<string>? Body { get; set; }

        [Column(Name = "BoundedBody", Json = true, Length = 200)]
        public List<string>? BoundedBody { get; set; }

        [Column(Name = "Hash", Length = 16)]
        public byte[]? Hash { get; set; }
    }
}

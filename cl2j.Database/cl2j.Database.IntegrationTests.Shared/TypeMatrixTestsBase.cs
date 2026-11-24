using System.Data.Common;
using System.Globalization;
using System.Reflection;
using cl2j.Database.DataAnnotations;
using cl2j.Database.TypeMatrix;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Every type the README claims to map, written and read back through the real driver: one
    ///     <c>Insert</c>, one <c>BulkInsert</c>, a read by key, a read of the whole table, and a
    ///     query by example on each of the three shapes of column (plain, required, nullable).
    ///     Each type has an entity of its own, so a failure names the type. Each case also checks
    ///     the type the server catalog reports for its columns.
    ///
    ///     <para>
    ///     The list of types is the same for every provider (an xUnit <c>MemberData</c> source is
    ///     static, so it cannot vary), and so are the values. What a provider's server does
    ///     differently goes through the hooks of <see cref="TypeTestsBase"/> (precision, a value
    ///     that cannot be stored, a value that comes back changed) and
    ///     <see cref="ExpectedColumnType"/>. Each hook is one method that takes the value or the
    ///     CLR type rather than one property per type, so a provider's overrides are a short list
    ///     of exceptions that calls <c>base</c> for the rest.
    ///     </para>
    /// </summary>
    public abstract class TypeMatrixTestsBase(IDatabaseFixture fixture) : TypeTestsBase(fixture)
    {
        public static TheoryData<string> Types => MatrixCases.Data();

        /// <summary>
        ///     The <c>DATA_TYPE</c> that <c>INFORMATION_SCHEMA.COLUMNS</c> reports for the column
        ///     a property of <paramref name="clrType"/> is created as. <paramref name="clrType"/>
        ///     is never <see cref="Nullable{T}"/> (already unwrapped); an enum arrives as itself.
        ///
        ///     <para>
        ///     SQL Server: <c>bool</c> bit, <c>byte</c> tinyint, <c>short</c> smallint, <c>int</c>
        ///     and enums int, <c>long</c> bigint, <c>float</c> real, <c>double</c> float,
        ///     <c>decimal</c> decimal, <c>Guid</c> uniqueidentifier, <c>string</c> varchar,
        ///     <c>byte[]</c> varbinary, <c>DateTime</c> datetime2, <c>DateTimeOffset</c>
        ///     datetimeoffset, <c>DateOnly</c> date, <c>TimeOnly</c> and <c>TimeSpan</c> time,
        ///     <c>char</c> char.
        ///     </para>
        ///
        ///     <para>
        ///     Override for what your catalog says, and call <c>base</c> for the types that agree.
        ///     Expected from the spikes: PostgreSQL reports <c>boolean</c>, <c>smallint</c> for
        ///     both <c>byte</c> and <c>short</c>, <c>integer</c> (int and enum), <c>bigint</c>,
        ///     <c>real</c>, <c>double precision</c>, <c>numeric</c>, <c>uuid</c>, <c>text</c>,
        ///     <c>bytea</c>, <c>timestamp without time zone</c>, <c>timestamp with time zone</c>,
        ///     <c>date</c>, <c>time without time zone</c>, <c>interval</c> (TimeSpan) and
        ///     <c>character</c>. MySQL reports <c>tinyint</c> (bool and byte), <c>smallint</c>,
        ///     <c>int</c>, <c>bigint</c>, <c>float</c>, <c>double</c>, <c>decimal</c>, <c>char</c>
        ///     (Guid and char), <c>longtext</c>, <c>longblob</c>, <c>datetime</c> (DateTime and
        ///     DateTimeOffset), <c>date</c> and <c>time</c> (TimeOnly and TimeSpan).
        ///     </para>
        /// </summary>
        /// <param name="clrType">The property type, with <see cref="Nullable{T}"/> removed.</param>
        protected virtual string ExpectedColumnType(Type clrType)
        {
            if (clrType.IsEnum)
                return "int";
            if (clrType == typeof(bool))
                return "bit";
            if (clrType == typeof(byte))
                return "tinyint";
            if (clrType == typeof(short))
                return "smallint";
            if (clrType == typeof(int))
                return "int";
            if (clrType == typeof(long))
                return "bigint";
            if (clrType == typeof(float))
                return "real";
            if (clrType == typeof(double))
                return "float";
            if (clrType == typeof(decimal))
                return "decimal";
            if (clrType == typeof(Guid))
                return "uniqueidentifier";
            if (clrType == typeof(string))
                return "varchar";
            if (clrType == typeof(byte[]))
                return "varbinary";
            if (clrType == typeof(DateTime))
                return "datetime2";
            if (clrType == typeof(DateTimeOffset))
                return "datetimeoffset";
            if (clrType == typeof(DateOnly))
                return "date";
            if (clrType == typeof(TimeOnly) || clrType == typeof(TimeSpan))
                return "time";
            if (clrType == typeof(char))
                return "char";
            throw new NotSupportedException($"No expected column type for {clrType}.");
        }

        [Theory]
        [MemberData(nameof(Types))]
        public async Task Insert_round_trips_a_value_of_every_shape(string type)
            => await MatrixCases.Get(type).Run(this, MatrixCases.Mode.InsertValues);

        [Theory]
        [MemberData(nameof(Types))]
        public async Task Insert_round_trips_null(string type)
            => await MatrixCases.Get(type).Run(this, MatrixCases.Mode.InsertNulls);

        [Theory]
        [MemberData(nameof(Types))]
        public async Task BulkInsert_round_trips_a_value_of_every_shape(string type)
            => await MatrixCases.Get(type).Run(this, MatrixCases.Mode.BulkValues);

        [Theory]
        [MemberData(nameof(Types))]
        public async Task BulkInsert_round_trips_null(string type)
            => await MatrixCases.Get(type).Run(this, MatrixCases.Mode.BulkNulls);

        [Theory]
        [MemberData(nameof(Types))]
        public async Task Query_by_example_finds_the_row_on_each_shape_of_column(string type)
            => await MatrixCases.Get(type).Run(this, MatrixCases.Mode.ByExample);

        //The cases reach the hooks through the owner they are run for: they are protected, and a
        //nested type is the one place besides the class itself that may call them.
        private static class MatrixCases
        {
            internal enum Mode { InsertValues, InsertNulls, BulkValues, BulkNulls, ByExample }

            private static readonly Dictionary<string, MatrixCase> All = Build();

            internal static MatrixCase Get(string name) => All[name];

            internal static TheoryData<string> Data()
            {
                var data = new TheoryData<string>();
                foreach (var name in All.Keys)
                    data.Add(name);
                return data;
            }

            private static Dictionary<string, MatrixCase> Build()
            {
                var cases = new List<MatrixCase>
                {
                    new MatrixCase<MxBool>("bool",
                        () => new() { V = true, R = true, N = true }, () => new() { V = false, R = false, N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxByte>("byte",
                        () => new() { V = 200, R = 255, N = 7 }, () => new() { V = 1, R = 0, N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxShort>("short",
                        () => new() { V = -12345, R = short.MaxValue, N = short.MinValue }, () => new() { V = 1, R = 0, N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxInt>("int",
                        () => new() { V = 42, R = -7, N = int.MaxValue }, () => new() { V = 1, R = 0, N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxLong>("long",
                        () => new() { V = long.MaxValue, R = long.MinValue, N = 5_000_000_000L }, () => new() { V = 1, R = 0, N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxFloat>("float",
                        () => new() { V = 1.5f, R = 3.4028235E+38f, N = -0.25f }, () => new() { V = 1f, R = 0f, N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxDouble>("double",
                        () => new() { V = 2.25, R = double.MaxValue, N = 0.1 }, () => new() { V = 1, R = 0, N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxDecimal>("decimal",
                        () => new() { V = 12.34m, R = -9999999999999999.99m, N = 0.01m }, () => new() { V = 1m, R = 0m, N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxGuid>("Guid",
                        () => new() { V = Guid.NewGuid(), R = Guid.NewGuid(), N = Guid.NewGuid() }, () => new() { V = Guid.NewGuid(), R = Guid.Empty, N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxString>("string",
                        () => new() { V = "hello", R = "café", N = "x" }, () => new() { V = null, R = "r", N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxBytes>("byte[]",
                        () => new() { V = [1, 2, 3], R = [0, 255], N = [9] }, () => new() { V = null, R = [], N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxDateTime>("DateTime",
                        () => new() { V = new DateTime(2026, 9, 27, 13, 14, 15).AddTicks(1234567), R = DateTime.MinValue, N = DateTime.MaxValue }, () => new() { V = new DateTime(2000, 1, 1), R = new DateTime(2001, 1, 1), N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxDateTimeOffset>("DateTimeOffset",
                        () => new() { V = new DateTimeOffset(2026, 9, 27, 13, 14, 15, TimeSpan.FromMinutes(330)), R = new DateTimeOffset(2020, 2, 29, 23, 59, 59, TimeSpan.FromHours(-8)), N = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero) },
                        () => new() { V = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), R = new DateTimeOffset(2001, 1, 1, 0, 0, 0, TimeSpan.Zero), N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxDateOnly>("DateOnly",
                        () => new() { V = new DateOnly(2026, 9, 27), R = DateOnly.MinValue, N = DateOnly.MaxValue }, () => new() { V = new DateOnly(2000, 1, 1), R = new DateOnly(2001, 1, 1), N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxTimeOnly>("TimeOnly",
                        () => new() { V = new TimeOnly(13, 14, 15).Add(TimeSpan.FromTicks(1234567)), R = TimeOnly.MinValue, N = TimeOnly.MaxValue }, () => new() { V = new TimeOnly(1, 1), R = new TimeOnly(2, 2), N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxTimeSpan>("TimeSpan",
                        () => new() { V = new TimeSpan(0, 13, 14, 15).Add(TimeSpan.FromTicks(1234567)), R = TimeSpan.Zero, N = new TimeSpan(1, 0, 0, 0).Subtract(TimeSpan.FromTicks(1)) }, () => new() { V = TimeSpan.FromHours(1), R = TimeSpan.FromHours(2), N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxChar>("char",
                        () => new() { V = 'Z', R = 'a', N = 'k' }, () => new() { V = 'q', R = 'r', N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                    new MatrixCase<MxKind>("enum",
                        () => new() { V = MatrixKind.One, R = MatrixKind.Big, N = MatrixKind.Zero }, () => new() { V = MatrixKind.Big, R = MatrixKind.One, N = null },
                        x => new { x.V }, x => new { x.R }, x => new { x.N }),
                };
                return cases.ToDictionary(c => c.Name);
            }
        }

        private abstract class MatrixCase(string name)
        {
            internal string Name { get; } = name;

            internal abstract Task Run(TypeMatrixTestsBase owner, MatrixCases.Mode mode);

            public override string ToString() => Name;
        }

        private sealed class MatrixCase<TRow>(
            string name,
            Func<TRow> values,
            Func<TRow> nulls,
            Func<TRow, object> exampleV,
            Func<TRow, object> exampleR,
            Func<TRow, object> exampleN) : MatrixCase(name)
            where TRow : class, new()
        {
            private static readonly string[] Shapes = ["V", "R", "N"];

            internal override async Task Run(TypeMatrixTestsBase owner, MatrixCases.Mode mode)
            {
                await using var connection = await owner.Fixture.OpenAsync();
                await connection.DropTableIfExists(typeof(TRow), CancellationToken.None);
                await connection.CreateTable<TRow>();
                try
                {
                    await AssertColumnTypes(owner, connection);

                    switch (mode)
                    {
                        case MatrixCases.Mode.InsertValues:
                            await RoundTrip(owner, connection, values(), bulk: false);
                            break;
                        case MatrixCases.Mode.InsertNulls:
                            await RoundTrip(owner, connection, nulls(), bulk: false);
                            break;
                        case MatrixCases.Mode.BulkValues:
                            await RoundTrip(owner, connection, values(), bulk: true);
                            break;
                        case MatrixCases.Mode.BulkNulls:
                            await RoundTrip(owner, connection, nulls(), bulk: true);
                            break;
                        case MatrixCases.Mode.ByExample:
                            await ByExample(owner, connection);
                            break;
                    }
                }
                finally
                {
                    await connection.DropTableIfExists(typeof(TRow), CancellationToken.None);
                }
            }

            //The three shapes of a case share one CLR type, so one expectation covers V, R and N.
            private static async Task AssertColumnTypes(TypeMatrixTestsBase owner, DbConnection connection)
            {
                var clrType = typeof(TRow).GetProperty("V")!.PropertyType;
                var expected = owner.ExpectedColumnType(Nullable.GetUnderlyingType(clrType) ?? clrType);
                var table = typeof(TRow).GetCustomAttribute<TableAttribute>()!.Name!;

                await using var command = connection.CreateCommand();
                command.CommandText = $"""
                    SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_NAME = '{table}'{owner.Fixture.CatalogScopeFilter}
                    """;
                var types = new Dictionary<string, string>(StringComparer.Ordinal);
                await using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        types[reader.GetString(0)] = reader.GetString(1);
                }

                foreach (var shape in Shapes)
                    Assert.Equal(expected, types[shape]);
            }

            //What the row is written with: each value trimmed to the provider's precision, then
            //replaced where the server cannot hold it.
            private static TRow Prepare(TypeMatrixTestsBase owner, TRow row)
            {
                foreach (var shape in Shapes)
                {
                    var property = typeof(TRow).GetProperty(shape)!;
                    if (property.GetValue(row) is { } value)
                        property.SetValue(row, owner.ValueToWrite(TemporalPrecision.Trim(value, owner.TemporalFractionalDigits)));
                }

                return row;
            }

            //What reading the written row back is expected to yield.
            private static TRow Expect(TypeMatrixTestsBase owner, TRow written)
            {
                var expected = new TRow();
                foreach (var shape in Shapes)
                {
                    var property = typeof(TRow).GetProperty(shape)!;
                    if (property.GetValue(written) is { } value)
                        property.SetValue(expected, owner.ExpectedValue(value));
                }

                return expected;
            }

            private static async Task RoundTrip(TypeMatrixTestsBase owner, DbConnection connection, TRow row, bool bulk)
            {
                var written = Prepare(owner, row);
                var expected = Expect(owner, written);

                int id;
                if (bulk)
                {
                    await connection.BulkInsert(new[] { written });
                    //The identity is the server's: the only way to learn it is to read the table.
                    id = (int)Get(Assert.Single(await connection.Query<TRow>()), "Id")!;
                }
                else
                    id = await connection.Insert<TRow, int>(written);

                //Two readers are generated per type: one for a list, one for a single row.
                var single = await connection.QueryKey<TRow>(id);
                var list = await connection.Query<TRow>();

                Assert.NotNull(single);
                Assert.Equal(Describe(expected), Describe(single));
                Assert.Equal(Describe(expected), Describe(Assert.Single(list)));
            }

            private async Task ByExample(TypeMatrixTestsBase owner, DbConnection connection)
            {
                var wanted = Prepare(owner, values());
                var other = Prepare(owner, nulls());
                //The examples are built from what the server will hold, which is also what it
                //compares the parameter with.
                var expected = Expect(owner, wanted);
                await connection.Insert(other);
                var id = await connection.Insert<TRow, int>(wanted);

                foreach (var (shape, example) in new[] { ("V", exampleV(expected)), ("R", exampleR(expected)), ("N", exampleN(expected)) })
                {
                    var found = await connection.Query<TRow>(example);
                    Assert.True(found.Count == 1 && (int)Get(found[0], "Id")! == id, $"Query by example on {shape} returned {found.Count} row(s)");
                    Assert.Equal(Describe(expected), Describe(found[0]));
                }
            }

            private static object? Get(TRow row, string property) => typeof(TRow).GetProperty(property)!.GetValue(row);

            private static string Describe(TRow row) => string.Join(" | ", Shapes.Select(s => Format(Get(row, s))));

            private static string Format(object? value) => value switch
            {
                null => "<null>",
                byte[] bytes => "0x" + Convert.ToHexString(bytes),
                DateTimeOffset moment => moment.ToString("o", CultureInfo.InvariantCulture),
                DateTime stamp => stamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture),
                TimeSpan span => span.ToString("c", CultureInfo.InvariantCulture),
                TimeOnly time => time.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
                DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                float single => single.ToString("R", CultureInfo.InvariantCulture),
                double number => number.ToString("R", CultureInfo.InvariantCulture),
                //The column keeps its declared scale (1.00), so the trailing zeros are not part of the value.
                decimal amount => amount.ToString("0.############################", CultureInfo.InvariantCulture),
                char letter => $"char:{(int)letter}",
                Enum e => $"{e.GetType().Name}.{e}",
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };
        }
    }
}

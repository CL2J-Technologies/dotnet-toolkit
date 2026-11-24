using System.Data.Common;
using System.Globalization;
using cl2j.Database.TypeMatrix;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Every type the README claims to map, written and read back through the real driver: one
    ///     <c>Insert</c>, one <c>BulkInsert</c>, a read by key, a read of the whole table, and a
    ///     query by example on each of the three shapes of column (plain, required, nullable).
    ///     Each type has an entity of its own, so a failure names the type.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class TypeMatrixTests(SqlServerFixture fixture)
    {
        public static TheoryData<string> Types => MatrixCases.Data();

        [Theory]
        [MemberData(nameof(Types))]
        public async Task Insert_round_trips_a_value_of_every_shape(string type)
            => await MatrixCases.Get(type).Run(fixture, MatrixCases.Mode.InsertValues);

        [Theory]
        [MemberData(nameof(Types))]
        public async Task Insert_round_trips_null(string type)
            => await MatrixCases.Get(type).Run(fixture, MatrixCases.Mode.InsertNulls);

        [Theory]
        [MemberData(nameof(Types))]
        public async Task BulkInsert_round_trips_a_value_of_every_shape(string type)
            => await MatrixCases.Get(type).Run(fixture, MatrixCases.Mode.BulkValues);

        [Theory]
        [MemberData(nameof(Types))]
        public async Task BulkInsert_round_trips_null(string type)
            => await MatrixCases.Get(type).Run(fixture, MatrixCases.Mode.BulkNulls);

        [Theory]
        [MemberData(nameof(Types))]
        public async Task Query_by_example_finds_the_row_on_each_shape_of_column(string type)
            => await MatrixCases.Get(type).Run(fixture, MatrixCases.Mode.ByExample);
    }

    internal static class MatrixCases
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

    internal abstract class MatrixCase(string name)
    {
        internal string Name { get; } = name;

        internal abstract Task Run(SqlServerFixture fixture, MatrixCases.Mode mode);

        public override string ToString() => Name;
    }

    internal sealed class MatrixCase<TRow>(
        string name,
        Func<TRow> values,
        Func<TRow> nulls,
        Func<TRow, object> exampleV,
        Func<TRow, object> exampleR,
        Func<TRow, object> exampleN) : MatrixCase(name)
        where TRow : class
    {
        private static readonly string[] Shapes = ["V", "R", "N"];

        internal override async Task Run(SqlServerFixture fixture, MatrixCases.Mode mode)
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(TRow), CancellationToken.None);
            await connection.CreateTable<TRow>();
            try
            {
                switch (mode)
                {
                    case MatrixCases.Mode.InsertValues:
                        await RoundTrip(connection, values(), bulk: false);
                        break;
                    case MatrixCases.Mode.InsertNulls:
                        await RoundTrip(connection, nulls(), bulk: false);
                        break;
                    case MatrixCases.Mode.BulkValues:
                        await RoundTrip(connection, values(), bulk: true);
                        break;
                    case MatrixCases.Mode.BulkNulls:
                        await RoundTrip(connection, nulls(), bulk: true);
                        break;
                    case MatrixCases.Mode.ByExample:
                        await ByExample(connection);
                        break;
                }
            }
            finally
            {
                await connection.DropTableIfExists(typeof(TRow), CancellationToken.None);
            }
        }

        private static async Task RoundTrip(DbConnection connection, TRow row, bool bulk)
        {
            int id;
            if (bulk)
            {
                await connection.BulkInsert(new[] { row });
                //The identity is the server's: the only way to learn it is to read the table.
                id = (int)Get(Assert.Single(await connection.Query<TRow>()), "Id")!;
            }
            else
                id = await connection.Insert<TRow, int>(row);

            //Two readers are generated per type: one for a list, one for a single row.
            var single = await connection.QueryKey<TRow>(id);
            var list = await connection.Query<TRow>();

            Assert.NotNull(single);
            Assert.Equal(Describe(row), Describe(single));
            Assert.Equal(Describe(row), Describe(Assert.Single(list)));
        }

        private async Task ByExample(DbConnection connection)
        {
            var wanted = values();
            var other = nulls();
            await connection.Insert(other);
            var id = await connection.Insert<TRow, int>(wanted);

            foreach (var (shape, example) in new[] { ("V", exampleV(wanted)), ("R", exampleR(wanted)), ("N", exampleN(wanted)) })
            {
                var found = await connection.Query<TRow>(example);
                Assert.True(found.Count == 1 && (int)Get(found[0], "Id")! == id, $"Query by example on {shape} returned {found.Count} row(s)");
                Assert.Equal(Describe(wanted), Describe(found[0]));
            }
        }

        private static object? Get(TRow row, string property) => typeof(TRow).GetProperty(property)!.GetValue(row);

        private static string Describe(TRow row) => string.Join(" | ", Shapes.Select(s => Format(Get(row, s))));

        internal static string Format(object? value) => value switch
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
            char letter =>$"char:{(int)letter}",
            Enum e => $"{e.GetType().Name}.{e}",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}

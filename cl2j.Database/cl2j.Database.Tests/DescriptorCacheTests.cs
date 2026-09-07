using System.Diagnostics;
using cl2j.Database.CommandBuilders;
using cl2j.Database.Descriptors;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     <see cref="TableDescriptorFactory"/> keeps a dictionary so a type is reflected over once.
    ///     It was not doing that: the value overload of <c>GetOrAdd</c> evaluates its argument
    ///     before the call, so the descriptor was rebuilt on every invocation and thrown away
    ///     whenever an entry already existed. See issue #29.
    /// </summary>
    public class DescriptorCacheTests
    {
        private static IDatabaseFormatter Formatter()
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            return CommandBuilderFactory.GetCommandBuilder(new SqlConnection()).DatabaseFormatter;
        }

        [Fact]
        public void A_type_already_described_is_not_described_again()
        {
            //A timing assertion, because the defect is about time and nothing else observes it.
            //The margin is not delicate: as written this took 12 µs a call, against 0.003 µs for a
            //dictionary lookup that hits. 100,000 calls is 1200 ms broken and under a millisecond
            //fixed, so the bound below fails the old behaviour six times over while leaving three
            //orders of magnitude of headroom on a slow runner.
            var formatter = Formatter();
            TableDescriptorFactory.Create(typeof(Invoice), formatter);

            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < 100_000; i++)
                TableDescriptorFactory.Create(typeof(Invoice), formatter);
            stopwatch.Stop();

            Assert.True(
                stopwatch.ElapsedMilliseconds < 200,
                $"100,000 cached lookups took {stopwatch.ElapsedMilliseconds} ms, which means the descriptor is being rebuilt.");
        }

        [Fact]
        public void The_same_type_gives_back_the_same_descriptor()
        {
            var formatter = Formatter();

            Assert.Same(
                TableDescriptorFactory.Create(typeof(Customer), formatter),
                TableDescriptorFactory.Create(typeof(Customer), formatter));
        }

        [Fact]
        public void A_different_formatter_gets_a_descriptor_of_its_own()
        {
            //The cache was keyed on Type alone, so two providers with different quoting would have
            //shared one descriptor: whichever ran first decided NameFormatted for both, and the
            //second would emit the first one's SQL. Not reachable with a single provider
            //registered, and wrong SQL rather than an error on the day a second one arrives.
            var descriptor = TableDescriptorFactory.Create(typeof(TenantScopedRow), Formatter());
            var other = TableDescriptorFactory.Create(typeof(TenantScopedRow), new BackquoteFormatter());

            Assert.Equal("[TenantScopedRow]", descriptor.NameFormatted);
            Assert.Equal("`TenantScopedRow`", other.NameFormatted);
        }

        /// <summary>
        ///     Quotes with backquotes rather than brackets, which is enough to tell two descriptors
        ///     apart. Nothing else on it is exercised.
        /// </summary>
        private sealed class BackquoteFormatter : IDatabaseFormatter
        {
            public string FormatTableName(string table, string? schema = null)
                => schema is null ? $"`{table}`" : $"`{schema}`.`{table}`";

            public string FormatColumnName(string name) => $"`{name}`";

            public string FormatParameterName(string name) => "@" + name;

            public string GetColumnDataType(ColumnDescriptor column) => "text";

            public string GetColumnKeyType(ColumnDescriptor column) => " NOT NULL";
        }
    }
}

using cl2j.Database.Databases;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     The timeout handed to the bulk copy. It used to read <see cref="TimeSpan.Seconds"/>, the
    ///     seconds component, instead of the whole duration: two minutes became 0 (no limit) and
    ///     90 seconds became 30.
    /// </summary>
    public class BulkInsertTimeoutTests
    {
        [Theory]
        [InlineData(90, 90)]
        [InlineData(120, 120)]
        [InlineData(3600, 3600)]
        [InlineData(5, 5)]
        public void The_whole_duration_is_used(int configuredSeconds, int expected)
        {
            var options = new DatabaseOptions { BulkInsertTimeout = TimeSpan.FromSeconds(configuredSeconds) };

            Assert.Equal(expected, options.GetBulkInsertTimeoutSeconds(15));
        }

        [Fact]
        public void A_fraction_of_a_second_rounds_up_rather_than_to_no_limit()
        {
            var options = new DatabaseOptions { BulkInsertTimeout = TimeSpan.FromMilliseconds(500) };

            Assert.Equal(1, options.GetBulkInsertTimeoutSeconds(15));
        }

        [Fact]
        public void Without_a_timeout_the_connection_timeout_is_used()
        {
            Assert.Equal(15, new DatabaseOptions().GetBulkInsertTimeoutSeconds(15));
        }

        [Fact]
        public void Zero_keeps_its_meaning_of_no_limit()
        {
            var options = new DatabaseOptions { BulkInsertTimeout = TimeSpan.Zero };

            Assert.Equal(0, options.GetBulkInsertTimeoutSeconds(15));
        }

        [Fact]
        public void A_timeout_too_long_for_an_int_is_capped()
        {
            var options = new DatabaseOptions { BulkInsertTimeout = TimeSpan.MaxValue };

            Assert.Equal(int.MaxValue, options.GetBulkInsertTimeoutSeconds(15));
        }
    }
}

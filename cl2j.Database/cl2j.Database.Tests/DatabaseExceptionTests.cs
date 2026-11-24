using cl2j.Database.Exceptions;
using Xunit;

namespace cl2j.Database.Tests
{
    public class DatabaseExceptionTests
    {
        [Fact]
        public void It_can_be_thrown_without_a_message()
        {
            Assert.Null(new DatabaseException().InnerException);
        }

        [Fact]
        public void The_message_is_kept()
        {
            Assert.Equal("nope", new DatabaseException("nope").Message);
        }

        [Fact]
        public void The_message_and_the_cause_are_kept()
        {
            var cause = new InvalidOperationException("root");

            var exception = new DatabaseException("nope", cause);

            Assert.Equal("nope", exception.Message);
            Assert.Same(cause, exception.InnerException);
        }
    }
}

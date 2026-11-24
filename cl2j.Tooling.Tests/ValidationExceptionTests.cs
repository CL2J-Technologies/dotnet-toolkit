using cl2j.Tooling.Exceptions;
using Xunit;

namespace cl2j.Tooling.Tests
{
    public class ValidationExceptionTests
    {
        [Fact]
        public void It_can_be_thrown_without_a_message()
        {
            Assert.Null(new ValidationException().InnerException);
        }

        [Fact]
        public void The_message_is_kept()
        {
            Assert.Equal("nope", new ValidationException("nope").Message);
        }

        [Fact]
        public void The_message_and_the_cause_are_kept()
        {
            var cause = new InvalidOperationException("root");

            var exception = new ValidationException("nope", cause);

            Assert.Equal("nope", exception.Message);
            Assert.Same(cause, exception.InnerException);
        }
    }
}

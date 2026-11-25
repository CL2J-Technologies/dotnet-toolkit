using cl2j.Tooling.Exceptions;
using Xunit;

namespace cl2j.Tooling.Tests
{
    /// <summary>
    ///     The exceptions callers throw and catch by type. What matters is that each keeps the
    ///     message and the inner exception it is given, so the cause is not lost on the way up.
    /// </summary>
    public class ExceptionTests
    {
        public static TheoryData<Func<string, Exception, Exception>> WithInner => new()
        {
            (m, i) => new BadRequestException(m, i),
            (m, i) => new ForbiddenException(m, i),
            (m, i) => new PropertyNotFoundException(m, i),
            (m, i) => new TooManyRequestsException(m, i),
            (m, i) => new UnauthorizedException(m, i),
            (m, i) => new UnhandledException(m, i),
        };

        public static TheoryData<Func<string, Exception>> WithMessage => new()
        {
            m => new BadRequestException(m),
            m => new ForbiddenException(m),
            m => new PropertyNotFoundException(m),
            m => new TooManyRequestsException(m),
            m => new UnauthorizedException(m),
            m => new UnhandledException(m),
        };

        public static TheoryData<Func<Exception>> Parameterless => new()
        {
            () => new BadRequestException(),
            () => new ForbiddenException(),
            () => new PropertyNotFoundException(),
            () => new TooManyRequestsException(),
            () => new UnauthorizedException(),
            () => new UnhandledException(),
        };

        [Theory]
        [MemberData(nameof(WithMessage))]
        public void The_message_is_kept(Func<string, Exception> create)
        {
            Assert.Equal("nope", create("nope").Message);
        }

        [Theory]
        [MemberData(nameof(WithInner))]
        public void The_message_and_the_cause_are_kept(Func<string, Exception, Exception> create)
        {
            var cause = new InvalidOperationException("root");

            var exception = create("nope", cause);

            Assert.Equal("nope", exception.Message);
            Assert.Same(cause, exception.InnerException);
        }

        [Theory]
        [MemberData(nameof(Parameterless))]
        public void It_can_be_thrown_without_a_message(Func<Exception> create)
        {
            Assert.Null(create().InnerException);
        }
    }
}

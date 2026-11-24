using cl2j.Scripting.Exceptions;
using Xunit;

namespace cl2j.Scripting.Tests
{
    /// <summary>
    ///     What a caller can recover from a <see cref="ScriptException"/>: the message, the cause,
    ///     and the source that failed to compile.
    /// </summary>
    public class ScriptExceptionTests
    {
        [Fact]
        public void It_can_be_thrown_without_details()
        {
            var exception = new ScriptException();

            Assert.Equal(string.Empty, exception.CompileSource);
            Assert.Null(exception.InnerException);
        }

        [Fact]
        public void The_message_and_the_failing_source_are_kept()
        {
            var exception = new ScriptException("does not compile", "return 1 +;");

            Assert.Equal("does not compile", exception.Message);
            Assert.Equal("return 1 +;", exception.CompileSource);
        }

        [Fact]
        public void The_cause_is_kept_with_the_message_and_the_source()
        {
            var cause = new InvalidOperationException("root");

            var exception = new ScriptException("does not run", "return 1;", cause);

            Assert.Equal("does not run", exception.Message);
            Assert.Equal("return 1;", exception.CompileSource);
            Assert.Same(cause, exception.InnerException);
        }

        [Fact]
        public void The_source_can_be_set_afterwards()
        {
            var exception = new ScriptException { CompileSource = "return 2;" };

            Assert.Equal("return 2;", exception.CompileSource);
        }
    }
}

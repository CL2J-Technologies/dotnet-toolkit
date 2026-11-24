using Microsoft.Extensions.Logging;
using Xunit;

namespace cl2j.Logging.Tests
{
    public class LoggerExtensionsTests
    {
        public static TheoryData<string, LogLevel> Levels => new()
        {
            { "Trace", LogLevel.Trace },
            { "Debug", LogLevel.Debug },
            { "Information", LogLevel.Information },
            { "Warning", LogLevel.Warning },
            { "Error", LogLevel.Error },
        };

        private static void Call(ILogger logger, string level, string message, params object[] args)
        {
            switch (level)
            {
                case "Trace": logger.LogTraceIfEnabled(message, args); break;
                case "Debug": logger.LogDebugIfEnabled(message, args); break;
                case "Information": logger.LogInformationIfEnabled(message, args); break;
                case "Warning": logger.LogWarningIfEnabled(message, args); break;
                case "Error": logger.LogErrorIfEnabled(message, args); break;
                default: throw new ArgumentOutOfRangeException(nameof(level));
            }
        }

        [Theory]
        [MemberData(nameof(Levels))]
        public void The_message_is_formatted_and_logged_at_its_level_when_enabled(string name, LogLevel level)
        {
            var logger = new CapturingLogger();

            Call(logger, name, "user {User} did {Action}", "ada", "login");

            Assert.Equal((level, "user ada did login"), Assert.Single(logger.Entries));
        }

        [Theory]
        [MemberData(nameof(Levels))]
        public void Nothing_is_logged_when_the_level_is_disabled(string name, LogLevel _)
        {
            var logger = new CapturingLogger(LogLevel.Critical);

            Call(logger, name, "never {Formatted}", "x");

            Assert.Empty(logger.Entries);
        }

        [Fact]
        public void The_arguments_are_not_evaluated_by_the_logger_when_the_level_is_disabled()
        {
            var logger = new CapturingLogger(LogLevel.Critical);
            var evaluated = false;
            var argument = new ToStringProbe(() => evaluated = true);

            logger.LogDebugIfEnabled("value {V}", argument);

            Assert.False(evaluated);
        }

        private sealed class ToStringProbe(Action onToString)
        {
            public override string ToString()
            {
                onToString();
                return "probe";
            }
        }
    }
}

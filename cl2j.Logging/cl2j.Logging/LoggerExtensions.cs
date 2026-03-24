#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.Logging;
#pragma warning restore IDE0130 // Namespace does not match folder structure


public static class LoggerExtensions
{
    public static void LogTraceIfEnabled(this ILogger logger, string message, params object[] args)
    {
        if (logger.IsEnabled(LogLevel.Trace))
            logger.LogTrace(message, args);
    }

    public static void LogDebugIfEnabled(this ILogger logger, string message, params object[] args)
    {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(message, args);
    }

    public static void LogInformationIfEnabled(this ILogger logger, string message, params object[] args)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation(message, args);
    }

    public static void LogWarningIfEnabled(this ILogger logger, string message, params object[] args)
    {
        if (logger.IsEnabled(LogLevel.Warning))
            logger.LogWarning(message, args);
    }

    public static void LogErrorIfEnabled(this ILogger logger, string message, params object[] args)
    {
        if (logger.IsEnabled(LogLevel.Error))
            logger.LogError(message, args);
    }
}

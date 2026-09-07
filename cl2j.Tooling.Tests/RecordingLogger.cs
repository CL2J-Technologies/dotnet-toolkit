using Microsoft.Extensions.Logging;

namespace cl2j.Tooling.Tests
{
    /// <summary>Keeps what was logged, so a test can assert that something was reported.</summary>
    public sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            //Written from timer callbacks as well as from the test thread.
            lock (Entries)
                Entries.Add((logLevel, formatter(state, exception), exception));
        }

        public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Snapshot()
        {
            lock (Entries)
                return [.. Entries];
        }
    }

    public sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly RecordingLogger inner = new();

        public List<(LogLevel Level, string Message, Exception? Exception)> Entries => inner.Entries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => inner.Log(logLevel, eventId, state, exception, formatter);
    }
}

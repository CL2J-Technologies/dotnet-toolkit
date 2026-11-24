using cl2j.Database.Databases;
using Microsoft.Extensions.Logging;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     What <see cref="BaseDatabase"/> does around a failing call, which is everything it can do
    ///     without a server: every operation opens a connection, and when that cannot work the
    ///     exception must be logged at the configured level and reach the caller unchanged. The
    ///     successful paths belong to the integration tests.
    /// </summary>
    public class BaseDatabaseTests
    {
        private sealed record LogEntry(LogLevel Level, Exception? Exception, string Message);

        private sealed class RecordingLogger(LogLevel minimum) : ILogger
        {
            public List<LogEntry> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= minimum;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
        }

        private sealed class UnsupportedDatabase(DatabaseOptions options, ILogger logger) : BaseDatabase(options, logger)
        {
            public override Task<System.Data.Common.DbConnection> CreateConnection()
                => Task.FromResult<System.Data.Common.DbConnection>(new UnsupportedConnection());
        }

        private sealed class BrokenDatabase(DatabaseOptions options, ILogger logger) : BaseDatabase(options, logger)
        {
            public override Task<System.Data.Common.DbConnection> CreateConnection()
                => throw new InvalidOperationException("no server");
        }

        public static TheoryData<string, Func<IDatabase, Task>> Operations => new()
        {
            { "Query", db => db.Query<Customer>() },
            { "Query(sql)", db => db.Query<Customer>("SELECT 1", new { Id = "a" }) },
            { "Query(param)", db => db.Query<Customer>(new { Id = "a" }) },
            { "QuerySingle(sql)", db => db.QuerySingle<Customer>("SELECT 1", new { Id = "a" }) },
            { "QuerySingle(param)", db => db.QuerySingle<Customer>(new { Id = "a" }) },
            { "QueryKeys", db => db.QueryKeys<Customer>(["a"]) },
            { "Exists", db => db.Exists<Customer>(new { Id = "a" }) },
            { "NewKey", db => db.NewKey<Customer>() },
            { "Insert", db => db.Insert(new Customer { Id = "a" }) },
            { "Update", db => db.Update(new Customer { Id = "a" }) },
            { "UpdateColumn", db => db.Update(new Customer { Id = "a" }, "Name", "b") },
            { "DeleteKey", db => db.DeleteKey<Customer>("a") },
            { "Execute", db => db.Execute("DELETE FROM Customer", new { Id = "a" }) },
        };

        [Theory]
        [MemberData(nameof(Operations))]
        public async Task A_failure_is_logged_at_the_exception_level_and_rethrown(string operation, Func<IDatabase, Task> call)
        {
            var logger = new RecordingLogger(LogLevel.Trace);
            var options = new DatabaseOptions { ExceptionLevel = LogLevel.Warning };

            var thrown = await Assert.ThrowsAnyAsync<Exception>(() => call(new UnsupportedDatabase(options, logger)));

            var entry = Assert.Single(logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Same(thrown, entry.Exception);
            Assert.Contains("Exception thrown", entry.Message, StringComparison.Ordinal);
            Assert.NotEmpty(operation);
        }

        [Theory]
        [MemberData(nameof(Operations))]
        public async Task A_failure_is_still_thrown_when_its_level_is_not_enabled(string operation, Func<IDatabase, Task> call)
        {
            var logger = new RecordingLogger(LogLevel.Critical);
            var options = new DatabaseOptions { ExceptionLevel = LogLevel.Debug };

            await Assert.ThrowsAnyAsync<Exception>(() => call(new UnsupportedDatabase(options, logger)));

            Assert.Empty(logger.Entries);
            Assert.NotEmpty(operation);
        }

        [Fact]
        public async Task A_failure_to_create_the_connection_reaches_the_caller_as_it_is()
        {
            var logger = new RecordingLogger(LogLevel.Trace);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new BrokenDatabase(new DatabaseOptions(), logger).Query<Customer>());

            Assert.Equal("no server", ex.Message);
            Assert.Same(ex, Assert.Single(logger.Entries).Exception);
        }

        [Fact]
        public async Task BuildSelectStatement_fails_without_logging_when_no_builder_supports_the_connection()
        {
            //Unlike the operations, building a statement does not catch: the caller sees the error
            //and nothing is written to the log.
            var logger = new RecordingLogger(LogLevel.Trace);

            await Assert.ThrowsAnyAsync<Exception>(() => new UnsupportedDatabase(new DatabaseOptions(), logger).BuildSelectStatement<Customer>());

            Assert.Empty(logger.Entries);
        }
    }
}

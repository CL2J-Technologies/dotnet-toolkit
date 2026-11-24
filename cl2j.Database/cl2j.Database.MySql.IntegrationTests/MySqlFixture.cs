using System.Data.Common;
using cl2j.Database.Databases;
using cl2j.Database.IntegrationTests;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Testcontainers.MySql;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    /// <summary>
    ///     One MySQL container for the whole assembly, started with <c>local_infile</c> on so that
    ///     both bulk insert strategies can be exercised against the same server.
    ///
    ///     <para>
    ///     The fixture itself is the batched-INSERT configuration (the plain connection string).
    ///     <see cref="BulkCopy"/> is the same server reached with <c>AllowLoadLocalInfile=true</c>,
    ///     which makes the provider use <c>MySqlBulkCopy</c>: the bases that bulk insert are run
    ///     once through each.
    ///     </para>
    /// </summary>
    public sealed class MySqlFixture : IAsyncLifetime, IDatabaseFixture
    {
        //8.4 is the current LTS. Pinned rather than floating, for the same reason the SQL Server
        //image is. local_infile is off by default since 8.0: the server has to allow what
        //MySqlBulkCopy asks of it.
        private readonly MySqlContainer container =
            new MySqlBuilder("mysql:8.4")
                .WithCommand("--local-infile=1")
                .Build();

        /// <summary>The plain connection string: bulk insert uses multi-row INSERT statements.</summary>
        public string ConnectionString { get; private set; } = string.Empty;

        /// <summary>With <c>AllowLoadLocalInfile=true</c>: bulk insert uses MySqlBulkCopy.</summary>
        public string BulkCopyConnectionString { get; private set; } = string.Empty;

        /// <summary>This fixture as seen through <see cref="BulkCopyConnectionString"/>.</summary>
        public IDatabaseFixture BulkCopy { get; private set; } = null!;

        public Type ConnectionType => typeof(MySqlConnection);

        public Type ServerExceptionType => typeof(MySqlException);

        //The catalog spans every database on the server.
        public string CatalogScopeFilter => " AND TABLE_SCHEMA = DATABASE()";

        //Typed as the interface on purpose: the facade tests exist to go through IDatabase.
        public IDatabase CreateDatabase(DatabaseOptions options, ILogger logger)
            => new MySqlDatabase(ConnectionString, options, logger);

        public async Task InitializeAsync()
        {
            cl2j.Database.MySql.MySql.Register();

            await container.StartAsync();
            ConnectionString = container.GetConnectionString();
            BulkCopyConnectionString = new MySqlConnectionStringBuilder(ConnectionString) { AllowLoadLocalInfile = true }.ConnectionString;
            BulkCopy = new BulkCopyView(this);
        }

        public async Task DisposeAsync() => await container.DisposeAsync();

        /// <summary>A new open connection, so a test that leaves a transaction open cannot poison the next.</summary>
        public Task<DbConnection> OpenAsync() => OpenAsync(ConnectionString);

        internal static async Task<DbConnection> OpenAsync(string connectionString)
        {
            var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }

        //The shared tests take an IDatabaseFixture; this one answers with the bulk copy connection
        //string everywhere the server is reached, and the container's answers for the rest.
        private sealed class BulkCopyView(MySqlFixture inner) : IDatabaseFixture
        {
            public string ConnectionString => inner.BulkCopyConnectionString;

            public Task<DbConnection> OpenAsync() => MySqlFixture.OpenAsync(inner.BulkCopyConnectionString);

            public IDatabase CreateDatabase(DatabaseOptions options, ILogger logger)
                => new MySqlDatabase(inner.BulkCopyConnectionString, options, logger);

            public Type ConnectionType => inner.ConnectionType;

            public Type ServerExceptionType => inner.ServerExceptionType;

            public string CatalogScopeFilter => inner.CatalogScopeFilter;
        }
    }

    [CollectionDefinition(Name)]
    public sealed class MySqlTestGroup : ICollectionFixture<MySqlFixture>
    {
        public const string Name = "mysql";
    }
}

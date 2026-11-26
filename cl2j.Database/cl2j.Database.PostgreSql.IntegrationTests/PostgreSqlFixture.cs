using System.Data.Common;
using cl2j.Database.Databases;
using cl2j.Database.IntegrationTests;
using Microsoft.Extensions.Logging;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace cl2j.Database.PostgreSql.IntegrationTests
{
    /// <summary>
    ///     One PostgreSQL container for the whole assembly. Like the SQL Server fixture it fails
    ///     rather than skips when Docker is unavailable: a skipped test reads as a passing one.
    /// </summary>
    public sealed class PostgreSqlFixture : IAsyncLifetime, IDatabaseFixture
    {
        //Pinned rather than floating: a suite that silently changes server version is one whose
        //failures cannot be reproduced. 17 is current.
        private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17").Build();

        public string ConnectionString { get; private set; } = string.Empty;

        public Type ConnectionType => typeof(NpgsqlConnection);

        public Type ServerExceptionType => typeof(PostgresException);

        //One database for the container, but several schemas: the catalog is narrowed to the one
        //the connection resolves unqualified names in, like the library does for TableExists.
        public string CatalogScopeFilter => " AND TABLE_SCHEMA = current_schema()";

        //Typed as the interface on purpose: the facade tests exist to go through IDatabase.
        public IDatabase CreateDatabase(DatabaseOptions options, ILogger logger)
            => new PostgreSqlDatabase(ConnectionString, options, logger);

        public async ValueTask InitializeAsync()
        {
            cl2j.Database.PostgreSql.PostgreSql.Register();

            await container.StartAsync();
            ConnectionString = container.GetConnectionString();
        }

        public async ValueTask DisposeAsync() => await container.DisposeAsync();

        public async Task<DbConnection> OpenAsync()
        {
            var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            return connection;
        }
    }

    [CollectionDefinition(Name)]
    public sealed class PostgreSqlGroup : ICollectionFixture<PostgreSqlFixture>
    {
        public const string Name = "postgresql";
    }
}

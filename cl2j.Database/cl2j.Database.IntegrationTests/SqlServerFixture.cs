using System.Data.Common;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     One SQL Server container for the whole assembly.
    ///
    ///     <para>
    ///     These tests exist because everything in cl2j.Database.Tests stops at the text of a
    ///     statement. Sixteen public operations on ConnectionExtensions, and the whole of
    ///     DbReaderExtensions, are only reachable with a server: that is where "the query ran but
    ///     the object is wrong" lives, and reading the code does not find that class of defect.
    ///     </para>
    ///
    ///     <para>
    ///     They fail rather than skip when Docker is unavailable. A skipped test reads as a passing
    ///     one in a summary line, and the skip tends to become permanent — the whole point here is
    ///     to stop trusting a green run that never exercised anything.
    ///     </para>
    /// </summary>
    public sealed class SqlServerFixture : IAsyncLifetime
    {
        private readonly MsSqlContainer container = new MsSqlBuilder()
            //Pinned rather than floating: a test suite that silently changes server version is a
            //suite whose failures cannot be reproduced.
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        public string ConnectionString { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            cl2j.Database.SqlServer.SqlServer.Register();

            await container.StartAsync();
            ConnectionString = container.GetConnectionString();
        }

        public async Task DisposeAsync() => await container.DisposeAsync();

        /// <summary>
        ///     A new open connection. Each test gets its own rather than sharing one, so a test
        ///     that leaves a transaction open cannot poison the next.
        /// </summary>
        public async Task<DbConnection> OpenAsync()
        {
            var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            return connection;
        }
    }

    [CollectionDefinition(Name)]
    public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
    {
        public const string Name = "sql-server";
    }
}

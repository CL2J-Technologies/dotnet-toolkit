using System.Data.Common;
using cl2j.Database.Databases;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace cl2j.Database.MySql
{
    /// <summary>
    ///     <see cref="IDatabase"/> over MySQL 8.0.13 or later, through MySqlConnector. Call
    ///     <see cref="MySql.Register"/> once before using it.
    /// </summary>
    /// <param name="connectionString">
    ///     A MySqlConnector connection string. Add <c>AllowLoadLocalInfile=true</c> to make bulk
    ///     inserts use <c>MySqlBulkCopy</c> (the server needs <c>local_infile=ON</c> too); without it
    ///     they use batched multi-row <c>INSERT</c> statements.
    /// </param>
    /// <param name="options">The database options.</param>
    /// <param name="logger">The logger.</param>
    public class MySqlDatabase(string connectionString, DatabaseOptions options, ILogger logger)
        : BaseDatabase(options, logger)
    {
        /// <inheritdoc />
        public override async Task<DbConnection> CreateConnection()
        {
            var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }
}

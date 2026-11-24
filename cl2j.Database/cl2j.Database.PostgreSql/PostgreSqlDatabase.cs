using System.Data.Common;
using cl2j.Database.Databases;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace cl2j.Database.PostgreSql
{
    public class PostgreSqlDatabase(string connectionString, DatabaseOptions options, ILogger logger)
        : BaseDatabase(options, logger)
    {
        public override async Task<DbConnection> CreateConnection()
        {
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }
}

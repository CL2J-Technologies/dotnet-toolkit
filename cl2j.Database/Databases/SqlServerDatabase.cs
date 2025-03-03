using System.Data.Common;
using cl2j.Database.Databases;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace cl2j.Database.DatabaseProviders
{
    public class SqlServerDatabase(string connectionString, DatabaseOptions options, ILogger logger) : BaseDatabase(options, logger)
    {
        protected override async Task<DbConnection> CreateConnection()
        {
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }
}

using System.Data;
using Dapper.Contrib.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Database
{
    public class SqlDatabaseRepository(string connectionString, ILogger logger) : DatabaseRepository(logger)
    {
        static SqlDatabaseRepository()
        {
            SqlMapperExtensions.TableNameMapper = (type) =>
            {
                return $"[{type.Name}]";
            };
        }

        protected override async Task<IDbConnection> CreateConnection()
        {
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }
}

using System.Data;
using System.Data.SqlClient;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Database
{
    [Obsolete("cl2j.DataStore.Database is deprecated. Use cl2j.Database with its SQL Server provider, cl2j.Database.SqlServer. It is not a drop-in: see the remarks on DatabaseRepository.")]
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

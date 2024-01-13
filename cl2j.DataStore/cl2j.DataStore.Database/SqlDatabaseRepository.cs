using System.Data;
using System.Data.SqlClient;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Logging;

namespace cl2j.DataStore.Database
{
    public class SqlDatabaseRepository : DatabaseRepository
    {
        private readonly string connectionString;

        static SqlDatabaseRepository()
        {
            SqlMapperExtensions.TableNameMapper = (type) =>
            {
                return $"[{type.Name}]";
            };
        }

        public SqlDatabaseRepository(string connectionString, ILogger logger)
            : base(logger)
        {
            this.connectionString = connectionString;
        }

        protected override async Task<IDbConnection> CreateConnection()
        {
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }
    }
}

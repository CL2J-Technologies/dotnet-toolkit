using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace cl2j.Database.CommandBuilders
{
    public class CommandBuilderFactory : ICommandBuilderFactory
    {
        public ICommandBuilder GetCommandBuilder(DbConnection connection)
        {
            var sqlConnection = connection as SqlConnection;
            if (sqlConnection is not null)
                return new SqlServerCommandBuilder();

            return new DefaultCommandBuilder();
        }
    }
}

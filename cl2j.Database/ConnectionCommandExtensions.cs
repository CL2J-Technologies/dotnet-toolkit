using System.Data;
using System.Data.Common;
using System.Diagnostics;
using cl2j.Database.CommandBuilders;

namespace cl2j.Database
{
    public static class ConnectionCommandExtensions
    {
        public static Task<bool> CreateTable(this DbConnection connection, Type type)
        {
            return CreateTable(connection, type, CancellationToken.None);
        }

        public static async Task<bool> CreateTable(this DbConnection connection, Type type, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            var commandBuilder = connection.GetCommandBuilder();
            var statement = commandBuilder.GetCreateTableStatement(type);
            Debug.WriteLine(statement);

            var cmd = CreateExecuteCommand(connection, statement, transaction);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }

        private static DbCommand CreateExecuteCommand(DbConnection connection, string sql, DbTransaction? transaction)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            return cmd;
        }
    }
}

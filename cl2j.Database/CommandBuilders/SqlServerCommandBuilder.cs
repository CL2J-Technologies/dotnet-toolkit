using System.Data.Common;
using cl2j.Database.CommandBuilders.Models;
using Microsoft.Data.SqlClient;

namespace cl2j.Database.CommandBuilders
{
    public class SqlServerCommandBuilder : BaseCommandBuilder, ICommandBuilder
    {
        public override bool Support(DbConnection connection)
        {
            return connection is SqlConnection;
        }

        public override string FormatTableName(string table, string? schema = null)
        {
            if (schema is null)
                return "[" + table + "]";
            return $"[{schema}].[{table}]";
        }

        public override string FormatColumnName(string column)
        {
            return "[" + column + "]";
        }

        public override string GetValueParameterName(string column)
        {
            return "@" + column;
        }

        public override TextStatement GetTableExistsStatement(Type type)
        {
            var tableDescriptor = GetTableDescriptor(type);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"SELECT TOP 1 * FROM {tableDescriptor.NameFormatted}"
            };
        }

        public override TextStatement GetDropTableIfExistsStatement(Type type)
        {
            var tableDescriptor = GetTableDescriptor(type);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"IF EXISTS(SELECT * FROM {tableDescriptor.NameFormatted}) DROP TABLE {tableDescriptor.NameFormatted}"
            };
        }
    }
}

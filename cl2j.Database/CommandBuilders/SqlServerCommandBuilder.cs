namespace cl2j.Database.CommandBuilders
{
    public class SqlServerCommandBuilder : BaseCommandBuilder, ICommandBuilder
    {
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
    }
}

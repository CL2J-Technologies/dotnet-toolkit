namespace cl2j.Database.Descriptors
{
    public interface IDatabaseFormatter
    {
        string FormatTableName(TableMetaData tableMetaData);
        string FormatTableName(string table, string? schema = null);

        string FormatColumnName(string name);

        string GetColumnDataType(ColumnDescriptor column);
        string GetColumnKeyType(ColumnDescriptor column);

        string FormatParameterName(string column);
    }
}

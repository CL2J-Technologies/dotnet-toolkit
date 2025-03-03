using System.Data.Common;
using System.Reflection;
using cl2j.Database.CommandBuilders.Models;

namespace cl2j.Database.CommandBuilders
{
    public interface ICommandBuilder
    {
        bool Support(DbConnection connection);

        TextStatement GetTableExistsStatement(Type type);
        TextStatement GetDropTableIfExistsStatement(Type type);
        TextStatement GetCreateTableStatement(Type type);
        TextStatement GetInsertStatement(Type type);
        TextStatement GetUpdateStatement(Type type);
        TextStatement GetDeleteStatement(Type type);
        TextStatement GetQueryStatement(Type type);

        string GetTableName(Type type, bool formatted = true);
        string FormatTableName(string table, string? schema = null);
        string GetColumnName(PropertyInfo propertyInfo, bool formatted = true);
        string FormatColumnName(string column);
        string GetValueParameterName(string column);
        string GetColumnKeyType(ColumnDescriptor column);

        TableDescriptor GetTableDescriptor(Type type);
        TableDescriptor CreateTableDescriptor(Type type);
        string GetColumnDataType(ColumnDescriptor column);
    }
}

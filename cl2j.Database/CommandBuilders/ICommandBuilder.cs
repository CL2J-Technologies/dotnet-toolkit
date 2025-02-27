using System.Reflection;

namespace cl2j.Database.CommandBuilders
{
    public interface ICommandBuilder
    {
        string GetCreateTableStatement(Type type);

        string GetTableName(Type type);
        string FormatTableName(string table, string? schema = null);
        string GetColumnName(PropertyInfo propertyInfo);
        string FormatColumnName(string column);

        IEnumerable<PropertyInfo> GetTableProperties(Type type);
        string GetColumnDataType(PropertyInfo propertyInfo);
        PropertyInfo? GetKeyProperty(IEnumerable<PropertyInfo> properties);
    }
}

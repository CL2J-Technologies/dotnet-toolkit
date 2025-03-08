using System.Reflection;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Helpers;

namespace cl2j.Database.Descriptors
{
    public static class DescriptorsHelpers
    {
        public static TableMetaData GetTableMetaData(this Type type)
        {
            var attr = type.GetAttribute<TableAttribute>();
            if (attr is not null && !string.IsNullOrEmpty(attr.Name))
                return new TableMetaData { Table = attr.Name, Schema = attr.Schema };

            return new TableMetaData { Table = type.Name };
        }

        public static IEnumerable<PropertyInfo> GetTableProperties(this Type type)
        {
            var properties = type.GetProperties().Where(x => x.CanRead && !x.HasAttribute<IgnoreAttribute>());
            return properties;
        }

        public static string GetColumnName(this PropertyInfo propertyInfo)
        {
            var attr = propertyInfo.GetAttribute<ColumnAttribute>();
            if (attr is not null && !string.IsNullOrEmpty(attr.Name))
                return attr.Name;

            return propertyInfo.Name;
        }

        public static List<ColumnDescriptor> GetKeys(this List<ColumnDescriptor> columns)
        {
            var list = new List<ColumnDescriptor>();

            foreach (var column in columns)
            {
                if (column.ColumnAtribute.Key != KeyType.None)
                    list.Add(column);
            }

            if (list.Count == 0)
            {
                var column = columns.FirstOrDefault(c => c.Name == "Id");
                if (column is not null)
                {
                    var attr = new ColumnAttribute
                    {
                        Name = column.ColumnAtribute.Name,
                        TypeName = column.ColumnAtribute.TypeName,
                        Length = column.ColumnAtribute.Length,
                        Decimals = column.ColumnAtribute.Decimals,
                        Default = column.ColumnAtribute.Default,
                        Required = column.ColumnAtribute.Required,
                        Json = column.ColumnAtribute.Json,
                        Key = KeyType.Key
                    };
                    column.ColumnAtribute = attr;
                    list.Add(column);
                }
            }

            return list;
        }

    }
}

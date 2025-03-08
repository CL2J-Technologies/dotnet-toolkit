using System.Collections.Concurrent;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Helpers;

namespace cl2j.Database.Descriptors
{
    public static class TableDescriptorFactory
    {
        private static readonly ColumnAttribute DefaultColumnAttribute = new();

        private static readonly ConcurrentDictionary<Type, TableDescriptor> TableDescriptors = [];

        public static TableDescriptor Create(Type type, IDatabaseFormatter formatter)
        {
            return TableDescriptors.GetOrAdd(type, o => InternalCreate(type, formatter));
        }

        private static TableDescriptor InternalCreate(Type type, IDatabaseFormatter formatter)
        {
            var properties = type.GetTableProperties();

            var columnsDescriptors = new List<ColumnDescriptor>();
            foreach (var property in properties)
            {
                var columnName = property.GetColumnName();
                columnsDescriptors.Add(new ColumnDescriptor
                {
                    Name = property.GetColumnName(),
                    NameFormatted = formatter.FormatColumnName(columnName),
                    Property = property,
                    ColumnAtribute = property.GetAttribute<ColumnAttribute>() ?? DefaultColumnAttribute
                });
            }

            var tableMetaData = type.GetTableMetaData();
            var tableDescriptor = new TableDescriptor
            {
                Name = tableMetaData.Table,
                NameFormatted = formatter.FormatTableName(tableMetaData),
                Keys = columnsDescriptors.GetKeys(),
                Columns = columnsDescriptors
            };
            return tableDescriptor;
        }
    }
}

using System.Collections.Concurrent;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Helpers;

namespace cl2j.Database.Descriptors
{
    public static class TableDescriptorFactory
    {
        private static readonly ColumnAttribute DefaultColumnAttribute = new();

        /// <summary>
        ///     Keyed on the formatter as well as the type, because the formatter decides every
        ///     quoted name in the descriptor. Keyed on the type alone, two providers with different
        ///     quoting would have shared one descriptor: whichever ran first would decide
        ///     <c>NameFormatted</c> for both, and the second would emit the first one's SQL. Not
        ///     reachable while only one provider exists, and wrong SQL rather than an error on the
        ///     day a second one is added. See issue #29.
        /// </summary>
        private static readonly ConcurrentDictionary<(Type Type, IDatabaseFormatter Formatter), TableDescriptor> TableDescriptors = [];

        public static TableDescriptor Create(Type type, IDatabaseFormatter formatter)
        {
            //The lambda overload, not the value one. C# evaluates arguments before the call, so
            //passing InternalCreate(...) directly ran the reflection on every invocation and threw
            //the result away whenever an entry already existed — 12 µs a call against 0.003 µs for
            //a lookup that hits. The dictionary was preventing a duplicate being stored, not the
            //work it exists to avoid.
            return TableDescriptors.GetOrAdd((type, formatter), key => InternalCreate(key.Type, key.Formatter));
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
                NameFormatted = formatter.FormatTableName(tableMetaData.Table, tableMetaData.Schema),
                Schema = tableMetaData.Schema,
                Keys = columnsDescriptors.GetKeys(),
                Columns = columnsDescriptors
            };
            return tableDescriptor;
        }
    }
}

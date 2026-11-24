using System.Data;
using cl2j.Database.Descriptors;

namespace cl2j.Database.Helpers
{
    public static class DataTableHelpers
    {
        public static DataTable CreateDataTable<T>(IEnumerable<T> items, string table, IEnumerable<ColumnDescriptor> columns)
        {
            var dataTable = new DataTable(table);

            //Columns
            foreach (var column in columns)
            {
                if (column.ColumnAtribute.Json)
                    dataTable.Columns.Add(column.Name, Types.TypeString);
                else
                {
                    var propertyType = Nullable.GetUnderlyingType(column.Property.PropertyType) ?? column.Property.PropertyType;
                    dataTable.Columns.Add(column.Name, propertyType);
                }
            }

            //Rows
            var colCount = columns.Count();
            foreach (var item in items)
            {
                var values = new object[colCount];

                int i = 0;
                foreach (var column in columns)
                {
                    var v = column.Property.GetValue(item);
                    //The same rule as Insert: a null DateTimeOffset with no column default becomes the
                    //current time, and a value is kept. This branch used to replace every value of a
                    //DateTimeOffset property by UtcNow, whatever the caller had set, offset included.
                    if (v is null)
                        values[i] = column.Property.PropertyType == Types.TypeDateTimeOffset && string.IsNullOrEmpty(column.ColumnAtribute.Default) ? DateTimeOffset.UtcNow : DBNull.Value;
                    else if (column.ColumnAtribute.Json)
                        values[i] = ConnectionExtensions.ToJsonString(v);
                    else
                        values[i] = v;

                    ++i;
                }

                dataTable.Rows.Add(values);
            }

            return dataTable;
        }
    }
}

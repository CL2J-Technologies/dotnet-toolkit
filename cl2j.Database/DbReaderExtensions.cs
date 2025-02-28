using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using cl2j.Database.CommandBuilders.Models;

namespace cl2j.Database
{
    public static class DbReaderExtensions
    {
        //System.Data.CommandBehavior.SingleRow
        private class ReaderMapping
        {
            public int Ordinal { get; set; }
            public PropertyInfo Property { get; set; } = null!;
            public bool Json { get; set; }
        }

        public static async Task<List<T>> Read<T>(this DbDataReader reader, TableDescriptor tableDescriptor, CancellationToken cancellationToken)
        {
            if (!reader.HasRows)
                return [];

            try
            {
                var mappings = GenerateMappings(reader, tableDescriptor);

                var list = new List<T>();
                while (await reader.ReadAsync(cancellationToken))
                    list.Add(Convert<T>(reader, mappings));
                return list;
            }
            finally
            {
                await reader.CloseAsync();
            }
        }

        public static async Task<T?> ReadSingle<T>(this DbDataReader reader, TableDescriptor tableDescriptor, CancellationToken cancellationToken)
        {
            if (!reader.HasRows)
                return default;

            try
            {
                var mappings = GenerateMappings(reader, tableDescriptor);

                if (await reader.ReadAsync(cancellationToken))
                    return Convert<T>(reader, mappings);
                return default;
            }
            finally
            {
                await reader.CloseAsync();
            }
        }

        private static T Convert<T>(DbDataReader reader, IEnumerable<ReaderMapping> mappings)
        {
            var o = Activator.CreateInstance<T>();

            foreach (var mapping in mappings)
            {
                var value = reader[mapping.Ordinal];

                if (mapping.Json)
                {
                    var data = value as string;
                    if (data is not null)
                    {
                        var type = mapping.Property.PropertyType;
                        value = JsonSerializer.Deserialize(data, type);
                    }
                }

                mapping.Property.SetValue(o, value);
            }

            return o;
        }

        private static IEnumerable<ReaderMapping> GenerateMappings(DbDataReader reader, TableDescriptor tableDescriptor)
        {
            var mappings = new List<ReaderMapping>(tableDescriptor.Columns.Count);
            foreach (var column in tableDescriptor.Columns)
            {
                var ordinal = reader.GetOrdinal(column.Name);
                mappings.Add(new ReaderMapping
                {
                    Ordinal = ordinal,
                    Property = column.Property,
                    Json = column.ColumnAtribute.Json
                });
            }

            return mappings.OrderBy(m => m.Ordinal);
        }
    }
}

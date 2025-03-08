using System.Collections.Concurrent;
using System.Data.Common;
using System.Reflection;
using System.Text;
using cl2j.Database.CommandBuilders.Models;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Helpers;

namespace cl2j.Database.CommandBuilders
{
    public abstract class BaseCommandBuilder : ICommandBuilder
    {
        private static readonly ColumnAttribute DefaultColumnAttribute = new();
        private static readonly ConcurrentDictionary<Type, TableDescriptor> TableDescriptors = [];

        public abstract bool Support(DbConnection connection);
        public abstract string FormatTableName(string table, string? schema = null);
        public abstract string FormatColumnName(string column);
        public abstract string GetValueParameterName(string column);
        public abstract string GetColumnDataType(ColumnDescriptor column);
        public abstract string GetColumnKeyType(ColumnDescriptor column);

        public abstract TextStatement GetTableExistsStatement(Type type);
        public abstract TextStatement GetDropTableStatement(Type type);

        public abstract Task InsertBatch<TIn>(DbConnection connection, IEnumerable<TIn> items, CancellationToken cancellationToken, DbTransaction? transaction = null);

        public TextStatement GetCreateTableStatement(Type type)
        {
            var tableDescriptor = GetTableDescriptor(type);

            var lines = new List<string>();
            foreach (var column in tableDescriptor.Columns)
            {
                var sbLine = new StringBuilder();
                sbLine.Append(column.NameFormatted);

                //Data Type
                string dataType = GetColumnDataType(column);
                sbLine.Append(" " + dataType);

                var isKey = tableDescriptor.IsKey(column);

                //Primary Key
                if (isKey)
                    sbLine.Append(GetColumnKeyType(column));

                //Required
                if (column.ColumnAtribute.Required)
                    sbLine.Append(" NOT NULL");

                lines.Add(sbLine.ToString());

                //Primary Key
                if (isKey)
                {
                    if (column.Property.PropertyType == Types.TypeString)
                        lines.Add($"CONSTRAINT {FormatColumnName($"PK_{tableDescriptor.Name}")} PRIMARY KEY CLUSTERED({column.NameFormatted} ASC)");
                }

                //Foreign Key
                var foreignKeyAttr = column.Property.GetAttribute<ForeignKeyAttribute>();
                if (foreignKeyAttr is not null)
                    lines.Add($"CONSTRAINT {FormatColumnName(foreignKeyAttr.Name)} FOREIGN KEY ({column.NameFormatted}) REFERENCES {FormatTableName(foreignKeyAttr.ReferenceTable)}({FormatColumnName(foreignKeyAttr.ReferenceField)})");
            }

            //Generate statement
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE {tableDescriptor.NameFormatted} ");
            sb.AppendLine("(");
            for (var i = 0; i < lines.Count; ++i)
                sb.AppendLine("\t" + (i > 0 ? "," : string.Empty) + lines[i]);
            sb.AppendLine(")");

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = sb.ToString()
            };
        }

        public TextStatement GetInsertStatement(Type type)
        {
            var tableDescriptor = GetTableDescriptor(type);

            var columns = tableDescriptor.Columns.Where(c => c.ColumnAtribute.Key != KeyType.Key);
            var fields = columns.Select(c => c.NameFormatted);
            var parameters = columns.Select(c => GetValueParameterName(c.Name));

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"INSERT INTO {tableDescriptor.NameFormatted} ({string.Join(',', fields)}) VALUES ({string.Join(',', parameters)})"
            };
        }

        public TextStatement GetUpdateStatement(Type type)
        {
            var tableDescriptor = GetTableDescriptor(type);

            var sbSet = new StringBuilder();
            var columnsUpdate = tableDescriptor.Columns.Where(c => c.ColumnAtribute.Key == KeyType.None);
            foreach (var column in columnsUpdate)
            {
                if (sbSet.Length > 0)
                    sbSet.Append(", ");
                sbSet.Append($"{column.NameFormatted}={GetValueParameterName(column.Name)}");
            }

            var where = GetTableKeysWhereClause(tableDescriptor);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"UPDATE {tableDescriptor.NameFormatted} SET {sbSet} WHERE {where}"
            };
        }

        public TextStatement GetDeleteStatement(Type type)
        {
            var tableDescriptor = GetTableDescriptor(type);
            var where = GetTableKeysWhereClause(tableDescriptor);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"DELETE FROM {tableDescriptor.NameFormatted} WHERE {where}"
            };
        }

        public TextStatement GetQueryStatement(Type type)
        {
            var tableDescriptor = GetTableDescriptor(type);
            var fields = tableDescriptor.Columns.Select(c => c.NameFormatted);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"SELECT {string.Join(',', fields)} FROM {tableDescriptor.NameFormatted}"
            };
        }

        public virtual string GetTableName(Type type, bool formatted = true)
        {
            var attr = type.GetAttribute<TableAttribute>();
            if (attr is not null && !string.IsNullOrEmpty(attr.Name))
            {
                if (formatted)
                    return FormatTableName(attr.Name, attr.Schema);
                return attr.Schema is null ? $"{attr.Name}" : $"{attr.Schema}.{attr.Name}";
            }

            return formatted ? FormatTableName(type.Name) : type.Name;
        }

        public virtual string GetColumnName(PropertyInfo propertyInfo, bool formatted = true)
        {
            string name;

            var attr = propertyInfo.GetAttribute<ColumnAttribute>();
            if (attr is not null && !string.IsNullOrEmpty(attr.Name))
                name = attr.Name;
            else
                name = propertyInfo.Name;

            return formatted ? FormatColumnName(name) : name;
        }

        public virtual TableDescriptor GetTableDescriptor(Type type)
        {
            return TableDescriptors.GetOrAdd(type, CreateTableDescriptor);
        }

        public virtual TableDescriptor CreateTableDescriptor(Type type)
        {
            var properties = PropertyHelpers.GetTableProperties(type);

            var columnsDescriptors = new List<ColumnDescriptor>();
            foreach (var property in properties)
            {
                columnsDescriptors.Add(new ColumnDescriptor
                {
                    Name = GetColumnName(property, false),
                    NameFormatted = GetColumnName(property, true),
                    Property = property,
                    ColumnAtribute = property.GetAttribute<ColumnAttribute>() ?? DefaultColumnAttribute
                });
            }

            var tableDescriptor = new TableDescriptor
            {
                Name = GetTableName(type, false),
                NameFormatted = GetTableName(type, true),
                Keys = GetKeys(columnsDescriptors),
                Columns = columnsDescriptors
            };
            return tableDescriptor;
        }

        public static List<ColumnDescriptor> GetKeys(List<ColumnDescriptor> columns)
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

        private string GetTableKeysWhereClause(TableDescriptor tableDescriptor)
        {
            var sb = new StringBuilder();
            foreach (var column in tableDescriptor.Keys)
            {
                if (sb.Length > 0)
                    sb.Append(" AND ");
                sb.Append($"{column.NameFormatted}={GetValueParameterName(column.Name)}");
            }

            return sb.ToString();
        }
    }
}

using System.Data.Common;
using System.Reflection;
using System.Text;
using cl2j.Database.CommandBuilders.Models;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Exceptions;
using cl2j.Database.Helpers;

namespace cl2j.Database.CommandBuilders
{
    public abstract class BaseCommandBuilder : ICommandBuilder
    {
        private static readonly ColumnAttribute DefaultColumnAttribute = new();

        public abstract bool Support(DbConnection connection);
        public abstract string FormatTableName(string table, string? schema = null);
        public abstract string FormatColumnName(string column);
        public abstract string GetValueParameterName(string column);

        public abstract TextStatement GetTableExistsStatement(Type type);
        public abstract TextStatement GetDropTableIfExistsStatement(Type type);

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
                {
                    if (column.Property.PropertyType == Types.TypeInt)
                        //TODO Envoyer dans SqlServer
                        //TODO Envoyer dans SqlServer
                        //TODO Envoyer dans SqlServer
                        sbLine.Append(" IDENTITY(1,1) PRIMARY KEY");
                    else if (column.Property.PropertyType == Types.TypeString)
                        sbLine.Append(" NOT NULL");
                    else if (column.Property.PropertyType == Types.TypeGuid)
                        //TODO Envoyer dans SqlServer
                        //TODO Envoyer dans SqlServer
                        //TODO Envoyer dans SqlServer
                        sbLine.Append(" UNIQUEIDENTIFIER DEFAULT NEWID() PRIMARY KEY");
                    else
                        throw new DatabaseException($"Unsupported key type '{column.Property.PropertyType.Name}'");
                }

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

        public InsertStatement GetInsertStatement(Type type)
        {
            var tableDescriptor = GetTableDescriptor(type);

            var columns = tableDescriptor.GetColumnsWithoutKey();
            var fields = columns.Select(c => c.NameFormatted);
            var parameters = columns.Select(c => GetValueParameterName(c.Name));

            return new InsertStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"INSERT INTO {tableDescriptor.NameFormatted} ({string.Join(',', fields)}) VALUES ({string.Join(',', parameters)})"
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

        public virtual string GetColumnDataType(ColumnDescriptor column)
        {
            var columnAttr = column.ColumnAtribute;
            var propertyInfo = column.Property;

            string propertyTypeDesc;

            if (!string.IsNullOrEmpty(columnAttr.TypeName))
                propertyTypeDesc = columnAttr.TypeName;
            else if (columnAttr.Json)
            {
                var length = columnAttr.Length <= 0 ? "max" : columnAttr.Length.ToString();
                propertyTypeDesc = $"varchar({length})";
            }
            else
            {
                if (propertyInfo.PropertyType == Types.TypeBool)
                    propertyTypeDesc = "bit";
                else if (propertyInfo.PropertyType == Types.TypeShort)
                    propertyTypeDesc = "smallint";
                else if (propertyInfo.PropertyType == Types.TypeInt || propertyInfo.PropertyType == Types.TypeLong)
                    propertyTypeDesc = "int";
                else if (propertyInfo.PropertyType == Types.TypeDecimal || propertyInfo.PropertyType == Types.TypeFloat || propertyInfo.PropertyType == Types.TypeDouble)
                {
                    if (columnAttr.Length > 0)
                        propertyTypeDesc = $"decimal({columnAttr.Length},{columnAttr.Decimals})";
                    else
                        propertyTypeDesc = "decimal";
                }
                else if (propertyInfo.PropertyType == Types.TypeString)
                {
                    if (columnAttr.Length > 0)
                        propertyTypeDesc = $"varchar({columnAttr.Length})";
                    else
                        propertyTypeDesc = $"varchar(max)";
                }
                else if (propertyInfo.PropertyType == Types.TypeDateTimeOffset)
                {
                    propertyTypeDesc = "datetimeoffset";
                    if (!string.IsNullOrEmpty(columnAttr.Default))
                        propertyTypeDesc += " DEFAULT SYSDATETIMEOFFSET()";
                }
                else if (propertyInfo.PropertyType == Types.TypeDateTime)
                    propertyTypeDesc = "datetime2";
                else
                    propertyTypeDesc = "varchar(MAX)";
            }

            if (!string.IsNullOrEmpty(columnAttr.Default))
                propertyTypeDesc += $" DEFAULT {columnAttr.Default}";

            return propertyTypeDesc;
        }

        public virtual TableDescriptor GetTableDescriptor(Type type)
        {
            return CreateTableDescriptor(type);
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

        public virtual List<ColumnDescriptor> GetColumnDescriptors(Type type)
        {
            var properties = PropertyHelpers.GetTableProperties(type);

            var list = new List<ColumnDescriptor>();
            foreach (var property in properties)
            {
                list.Add(new ColumnDescriptor
                {
                    Name = GetColumnName(property, false),
                    NameFormatted = GetColumnName(property, true),
                    Property = property,
                    ColumnAtribute = property.GetAttribute<ColumnAttribute>() ?? DefaultColumnAttribute
                });
            }
            return list;
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
                    column.ColumnAtribute.Key = KeyType.Key;
                    list.Add(column);
                }
            }

            return list;
        }
    }
}

using System.Data.Common;
using cl2j.Database.CommandBuilders.Models;
using cl2j.Database.Exceptions;
using cl2j.Database.Helpers;
using Microsoft.Data.SqlClient;

namespace cl2j.Database.CommandBuilders
{
    public class SqlServerCommandBuilder : BaseCommandBuilder, ICommandBuilder
    {
        public override bool Support(DbConnection connection)
        {
            return connection is SqlConnection;
        }

        public override string FormatTableName(string table, string? schema = null)
        {
            if (schema is null)
                return "[" + table + "]";
            return $"[{schema}].[{table}]";
        }

        public override string FormatColumnName(string column)
        {
            return "[" + column + "]";
        }

        public override string GetValueParameterName(string column)
        {
            return "@" + column;
        }

        public override string GetColumnDataType(ColumnDescriptor column)
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
                    propertyTypeDesc = "datetimeoffset";
                else if (propertyInfo.PropertyType == Types.TypeDateTime)
                    propertyTypeDesc = "datetime2";
                else
                    propertyTypeDesc = "varchar(MAX)";
            }

            if (!string.IsNullOrEmpty(columnAttr.Default))
                propertyTypeDesc += $" DEFAULT {columnAttr.Default}";

            return propertyTypeDesc;
        }

        public override string GetColumnKeyType(ColumnDescriptor column)
        {
            if (column.Property.PropertyType == Types.TypeInt)
                return " IDENTITY(1,1) PRIMARY KEY";
            else if (column.Property.PropertyType == Types.TypeString)
                return " NOT NULL";
            else if (column.Property.PropertyType == Types.TypeGuid)
                return " UNIQUEIDENTIFIER DEFAULT NEWID() PRIMARY KEY";

            throw new DatabaseException($"Unsupported key type '{column.Property.PropertyType.Name}'");
        }

        public override TextStatement GetTableExistsStatement(Type type)
        {
            var tableDescriptor = GetTableDescriptor(type);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"SELECT TOP 1 * FROM {tableDescriptor.NameFormatted}"
            };
        }

        public override TextStatement GetDropTableIfExistsStatement(Type type)
        {
            var tableDescriptor = GetTableDescriptor(type);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"IF EXISTS(SELECT * FROM {tableDescriptor.NameFormatted}) DROP TABLE {tableDescriptor.NameFormatted}"
            };
        }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Helpers;

namespace cl2j.Database.CommandBuilders
{
    public abstract class BaseCommandBuilder : ICommandBuilder
    {
        public string GetCreateTableStatement(Type type)
        {
            var tableName = GetTableName(type);
            var properties = GetTableProperties(type);
            var keyProperty = GetKeyProperty(properties);

            var lines = new List<string>();
            foreach (var property in properties)
            {
                var sbLine = new StringBuilder();

                var columnName = GetColumnName(property);
                sbLine.Append(columnName);

                //Data Type
                string dataType = GetColumnDataType(property);
                sbLine.Append(" " + dataType);

                //Primary Key
                if (property == keyProperty)
                {
                    if (property.PropertyType == Types.TypeInt)
                        sbLine.Append(" IDENTITY(1,1) PRIMARY KEY");
                    else if (property.PropertyType == Types.TypeString)
                        sbLine.Append(" NOT NULL");
                    else if (property.PropertyType == Types.TypeGuid)
                        sbLine.Append(" UNIQUEIDENTIFIER DEFAULT NEWID() PRIMARY KEY");
                    else
                        throw new DatabaseException($"Unsupported key type '{property.PropertyType.Name}'");
                }

                //Required
                var attrRequired = type.GetAttribute<RequiredAttribute>();
                if (attrRequired is not null)
                    sbLine.Append(" NOT NULL");

                lines.Add(sbLine.ToString());
            }

            //Constraints
            foreach (var property in properties)
            {
                //Primary Key
                if (property == keyProperty)
                {
                    if (property.PropertyType == Types.TypeString)
                        lines.Add($"CONSTRAINT {FormatColumnName($"PK_{tableName}")} PRIMARY KEY CLUSTERED({GetColumnName(property)} ASC)");
                }

                //Foreign Key
                var foreignKeyAttr = type.GetAttribute<DataAnnotations.ForeignKeyAttribute>();
                if (foreignKeyAttr is not null)
                    lines.Add($"CONSTRAINT {FormatColumnName(foreignKeyAttr.Name)} FOREIGN KEY ({GetColumnName(property)}) REFERENCES {FormatTableName(foreignKeyAttr.ReferenceTable)}([Id])");
            }

            //Generate statement
            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE {tableName} ");
            sb.AppendLine("(");
            for (var i = 0; i < lines.Count; ++i)
                sb.AppendLine("\t" + (i > 0 ? "," : string.Empty) + lines[i]);
            sb.AppendLine(")");

            return sb.ToString();
        }

        public virtual string GetTableName(Type type)
        {
            var attr = type.GetAttribute<TableAttribute>();
            if (attr is not null && !string.IsNullOrEmpty(attr.Name))
                return FormatTableName(attr.Name, attr.Schema);

            return FormatTableName(type.Name);
        }

        public abstract string FormatTableName(string table, string? schema = null);

        public virtual string GetColumnName(PropertyInfo propertyInfo)
        {
            var attr = propertyInfo.GetAttribute<ColumnAttribute>();
            if (attr is not null && !string.IsNullOrEmpty(attr.Name))
                return FormatColumnName(attr.Name);

            return FormatColumnName(propertyInfo.Name);
        }

        public abstract string FormatColumnName(string column);

        public string GetColumnDataType(PropertyInfo propertyInfo)
        {
            string propertyTypeDesc;

            var attrColumn = propertyInfo.GetAttribute<ColumnAttribute>();
            if (attrColumn is not null && !string.IsNullOrEmpty(attrColumn.TypeName))
                propertyTypeDesc = attrColumn.TypeName;
            else if (propertyInfo.HasAttribute<JsonDataTypeAttribute>())
                propertyTypeDesc = "nvarchar(max)";
            else
            {
                if (propertyInfo.PropertyType == Types.TypeShort)
                    propertyTypeDesc = "smallint";
                else if (propertyInfo.PropertyType == Types.TypeInt || propertyInfo.PropertyType == Types.TypeLong)
                    propertyTypeDesc = "int";
                else if (propertyInfo.PropertyType == Types.TypeDecimal || propertyInfo.PropertyType == Types.TypeFloat || propertyInfo.PropertyType == Types.TypeDouble)
                {
                    var attrPrecision = propertyInfo.GetAttribute<PrecisionAttribute>();
                    if (attrPrecision is not null)
                        propertyTypeDesc = $"decimal({attrPrecision.Number},{attrPrecision.Precision})";
                    else
                        propertyTypeDesc = "decimal";
                }
                else if (propertyInfo.PropertyType == Types.TypeString)
                {
                    var attrLength = propertyInfo.GetAttribute<MaxLengthAttribute>();
                    if (attrLength is not null)
                        propertyTypeDesc = $"nvarchar({attrLength.Length})";
                    else
                        propertyTypeDesc = $"nvarchar(max)";
                }
                else if (propertyInfo.PropertyType == Types.TypeDateTimeOffset)
                    propertyTypeDesc = "datetimeoffset";
                else if (propertyInfo.PropertyType == Types.TypeDateTime)
                    propertyTypeDesc = "datetime2";
                else
                    propertyTypeDesc = "varchar(MAX)";
            }

            return propertyTypeDesc;
        }

        public IEnumerable<PropertyInfo> GetTableProperties(Type type)
        {
            return PropertyHelpers.GetTableProperties(type);
        }

        public PropertyInfo? GetKeyProperty(IEnumerable<PropertyInfo> properties)
        {
            foreach (var property in properties)
            {
                if (property.HasAttribute<KeyAttribute>())
                    return property;
            }

            foreach (var property in properties)
            {
                var propertyName = GetColumnName(property);
                if (propertyName == "Id")
                    return property;
            }

            return null;
        }
    }
}

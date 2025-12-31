using System.Text;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Helpers;

namespace cl2j.Database.CommandBuilders
{
    public abstract class CommandBuilderHelpers
    {
        public static TextStatement GetCreateTableStatement(Type type, IDatabaseFormatter formatter)
        {
            var tableDescriptor = TableDescriptorFactory.Create(type, formatter);

            var lines = new List<string>();
            foreach (var column in tableDescriptor.Columns)
            {
                var sbLine = new StringBuilder();
                sbLine.Append(column.NameFormatted);

                //Data Type
                string dataType = formatter.GetColumnDataType(column);
                sbLine.Append(" " + dataType);

                var isKey = tableDescriptor.IsKey(column);

                //Primary Key
                if (isKey)
                    sbLine.Append(formatter.GetColumnKeyType(column));

                //Required
                if (column.ColumnAtribute.Required || column.Property.PropertyType.IsEnum)
                    sbLine.Append(" NOT NULL");

                lines.Add(sbLine.ToString());

                //Primary Key
                if (isKey)
                {
                    if (column.Property.PropertyType == Types.TypeString)
                        lines.Add($"CONSTRAINT {formatter.FormatColumnName($"PK_{tableDescriptor.Name}")} PRIMARY KEY CLUSTERED({column.NameFormatted} ASC)");
                }

                //Foreign Key
                var foreignKeyAttr = column.Property.GetAttribute<ForeignKeyAttribute>();
                if (foreignKeyAttr is not null)
                    lines.Add($"CONSTRAINT {formatter.FormatColumnName(foreignKeyAttr.Name)} FOREIGN KEY ({column.NameFormatted}) REFERENCES {formatter.FormatTableName(foreignKeyAttr.ReferenceTable, foreignKeyAttr.ReferenceSchema)}({formatter.FormatColumnName(foreignKeyAttr.ReferenceField)})");
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

        public static TextStatement GetDropTableStatement(Type type, IDatabaseFormatter formatter)
        {
            var tableDescriptor = TableDescriptorFactory.Create(type, formatter);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"DROP TABLE {tableDescriptor.NameFormatted}"
            };
        }

        public static TextStatement GetInsertStatement(Type type, IDatabaseFormatter formatter)
        {
            var tableDescriptor = TableDescriptorFactory.Create(type, formatter);

            var columns = tableDescriptor.Columns.Where(c => c.ColumnAtribute.Key != KeyType.Key);
            var fields = columns.Select(c => c.NameFormatted);
            var parameters = columns.Select(c => formatter.FormatParameterName(c.Name));

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"INSERT INTO {tableDescriptor.NameFormatted} ({string.Join(',', fields)}) VALUES ({string.Join(',', parameters)})"
            };
        }

        public static TextStatement GetUpdateStatement(Type type, IDatabaseFormatter formatter)
        {
            var tableDescriptor = TableDescriptorFactory.Create(type, formatter);

            var sbSet = new StringBuilder();
            var columnsUpdate = tableDescriptor.Columns.Where(c => c.ColumnAtribute.Key == KeyType.None);
            foreach (var column in columnsUpdate)
            {
                if (sbSet.Length > 0)
                    sbSet.Append(", ");
                sbSet.Append($"{column.NameFormatted}={formatter.FormatParameterName(column.Name)}");
            }

            var where = GetColumnWhereClause(tableDescriptor.Keys, formatter);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"UPDATE {tableDescriptor.NameFormatted} SET {sbSet} WHERE {where}"
            };
        }

        public static TextStatement GetUpdateColumnStatement(Type type, string columnName, IDatabaseFormatter formatter)
        {
            var tableDescriptor = TableDescriptorFactory.Create(type, formatter);
            var column = tableDescriptor.Columns.FirstOrDefault(c => c.Name == columnName && c.ColumnAtribute.Key == KeyType.None) ?? throw new ArgumentException($"Column '{columnName}' not found or is a key column.");

            var updateColumnSql = $"{column.NameFormatted}={formatter.FormatParameterName(column.Name)}";
            var where = GetColumnWhereClause(tableDescriptor.Keys, formatter);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"UPDATE {tableDescriptor.NameFormatted} SET {updateColumnSql} WHERE {where}"
            };
        }

        public static TextStatement GetDeleteStatement(Type type, IDatabaseFormatter formatter)
        {
            var tableDescriptor = TableDescriptorFactory.Create(type, formatter);
            var where = GetColumnWhereClause(tableDescriptor.Keys, formatter);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"DELETE FROM {tableDescriptor.NameFormatted} WHERE {where}"
            };
        }

        public static TextStatement GetQueryStatement(Type type, IDatabaseFormatter formatter)
        {
            var tableDescriptor = TableDescriptorFactory.Create(type, formatter);
            var fields = tableDescriptor.Columns.Select(c => c.NameFormatted);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"SELECT {string.Join(',', fields)} FROM {tableDescriptor.NameFormatted}"
            };
        }

        public static TextStatement GetQueryStatement(Type type, Type paramType, IDatabaseFormatter formatter)
        {
            var statement = GetQueryStatement(type, formatter);

            var tableDescriptorParam = TableDescriptorFactory.Create(paramType, formatter);
            var where = GetColumnWhereClause(tableDescriptorParam.Columns, formatter);
            if (where.Length > 0)
                statement.Text += " WHERE " + where;

            return statement;
        }

        private static string GetColumnWhereClause(List<ColumnDescriptor> columns, IDatabaseFormatter formatter)
        {
            var sb = new StringBuilder();
            foreach (var column in columns)
            {
                if (sb.Length > 0)
                    sb.Append(" AND ");
                sb.Append($"{column.NameFormatted}={formatter.FormatParameterName(column.Name)}");
            }

            return sb.ToString();
        }
    }
}

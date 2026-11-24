using System.Text;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Exceptions;
using cl2j.Database.Helpers;

namespace cl2j.Database.CommandBuilders
{
    public abstract class CommandBuilderHelpers
    {
        public static TextStatement GetCreateTableStatement(Type type, IDatabaseFormatter formatter)
        {
            var tableDescriptor = TableDescriptorFactory.Create(type, formatter);

            //More than one key column is a composite primary key: one table-level constraint over
            //all of them. A PRIMARY KEY on each column would make the server refuse the table
            //("multiple primary keys").
            var compositeKey = tableDescriptor.Keys.Count > 1;

            //A string with no Length is varchar(max) (longtext, text), which SQL Server and MySQL
            //refuse as a key and which no provider should index by accident. Refuse it here, with
            //the property and the fix, the way a decimal without precision is refused; the server's
            //own message names neither. An explicit TypeName is the caller's own choice.
            foreach (var key in tableDescriptor.Keys)
            {
                var keyType = Nullable.GetUnderlyingType(key.Property.PropertyType) ?? key.Property.PropertyType;
                if (keyType == Types.TypeString && key.ColumnAtribute.Length <= 0 && string.IsNullOrEmpty(key.ColumnAtribute.TypeName))
                    throw new DatabaseException($"{tableDescriptor.Name}.{key.Property.Name}: A string key needs a Length: [Column(Length = 50)]");
            }

            var lines = new List<string>();
            foreach (var column in tableDescriptor.Columns)
            {
                var sbLine = new StringBuilder();
                sbLine.Append(column.NameFormatted);

                //Data Type
                string dataType = formatter.GetColumnDataType(column);
                sbLine.Append(" " + dataType);

                var isKey = tableDescriptor.IsKey(column);
                var required = column.ColumnAtribute.Required || column.Property.PropertyType.IsEnum;

                //Primary Key
                if (isKey && !compositeKey)
                    sbLine.Append(formatter.GetColumnKeyType(column));
                else if (isKey && !required)
                    //A column of a composite key is plain: the caller supplies it, so it has no
                    //identity and no server default, and it cannot be NULL. The constraint that
                    //joins the columns is written once, after the last column.
                    sbLine.Append(" NOT NULL");

                //Required
                if (required)
                    sbLine.Append(" NOT NULL");

                lines.Add(sbLine.ToString());

                //Primary Key
                if (isKey && !compositeKey)
                {
                    //Plain PRIMARY KEY, with neither CLUSTERED nor ASC: MySQL refuses the first and
                    //PostgreSQL refuses the second, and neither is needed. On SQL Server a primary key
                    //on a new table is clustered by default, and ascending is the default order, so
                    //the table comes out the same.
                    if (column.Property.PropertyType == Types.TypeString)
                        lines.Add($"CONSTRAINT {formatter.FormatColumnName($"PK_{tableDescriptor.Name}")} PRIMARY KEY ({column.NameFormatted})");
                }

                //Foreign Key
                var foreignKeyAttr = column.Property.GetAttribute<ForeignKeyAttribute>();
                if (foreignKeyAttr is not null)
                    lines.Add($"CONSTRAINT {formatter.FormatColumnName(foreignKeyAttr.Name)} FOREIGN KEY ({column.NameFormatted}) REFERENCES {formatter.FormatTableName(foreignKeyAttr.ReferenceTable, foreignKeyAttr.ReferenceSchema)}({formatter.FormatColumnName(foreignKeyAttr.ReferenceField)})");
            }

            if (compositeKey)
                lines.Add($"CONSTRAINT {formatter.FormatColumnName($"PK_{tableDescriptor.Name}")} PRIMARY KEY ({string.Join(", ", tableDescriptor.Keys.Select(k => k.NameFormatted))})");

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

            var columns = tableDescriptor.Columns.Where(c => !IsSuppliedByTheServer(c));
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

        /// <summary>
        ///     True when the database fills the column in by itself, so an INSERT must leave it out.
        ///
        ///     <para>
        ///     The test used to be <c>Key != KeyType.Key</c> — every declared key was left out.
        ///     That is right for an <c>int</c> key, which the DDL declares <c>IDENTITY(1,1)</c>,
        ///     and for a <c>Guid</c> one, declared <c>DEFAULT NEWID()</c>. It is wrong for a string
        ///     key: nothing generates that value, so the column was dropped from the INSERT and the
        ///     NOT NULL primary key this library had just created rejected the row.
        ///     </para>
        ///
        ///     <para>
        ///     <c>KeyType.Key</c> is the obvious annotation to reach for on a primary key, so the
        ///     failure landed on the most natural reading of the API — and the server named the
        ///     column rather than the annotation, which made the trail back a long one. See issue
        ///     #26.
        ///     </para>
        ///
        ///     <para>
        ///     The CLR types listed here mirror what <c>GetColumnKeyType</c> emits. A provider that
        ///     declares its keys differently would need this to follow.
        ///     </para>
        ///
        ///     <para>
        ///     Public so a provider's bulk insert leaves out the same columns as INSERT. SQL Server's
        ///     used to leave out every <c>KeyType.Key</c> column, string keys included, so a bulk insert
        ///     of a string-keyed entity sent no key and the NOT NULL primary key rejected it.
        ///     </para>
        /// </summary>
        public static bool IsSuppliedByTheServer(ColumnDescriptor column)
        {
            if (column.ColumnAtribute.Key != KeyType.Key)
                return false;

            //A server-generated key only makes sense alone: each column of a composite key is
            //supplied by the caller and declared NOT NULL with no identity and no default.
            if (column.IsPartOfCompositeKey)
                return false;

            var type = column.Property.PropertyType;

            return type == Types.TypeInt
                || type == Types.TypeLong
                || type == Types.TypeGuid;
        }

        /// <summary>
        ///     True when the column is an identity key: <c>KeyType.Key</c> on an <c>int</c> or a
        ///     <c>long</c>, which is what the DDL declares as an auto-incremented primary key.
        ///
        ///     <para>
        ///     Narrower than <see cref="IsSuppliedByTheServer"/>, which also covers a <c>Guid</c> key.
        ///     A Guid comes from a column default and no "last identity" function reports it, so only
        ///     an identity column has a value to read back after an insert.
        ///     </para>
        /// </summary>
        public static bool IsIdentity(ColumnDescriptor column)
        {
            if (column.ColumnAtribute.Key != KeyType.Key)
                return false;

            //No identity inside a composite key. See IsSuppliedByTheServer.
            if (column.IsPartOfCompositeKey)
                return false;

            var type = column.Property.PropertyType;

            return type == Types.TypeInt || type == Types.TypeLong;
        }

        public static TextStatement GetQueryByKeyStatement(Type type, IDatabaseFormatter formatter)
        {
            var statement = GetQueryStatement(type, formatter);

            var columnKeys = statement.TableDescriptor.Columns.Where(c => c.ColumnAtribute.Key != KeyType.None);
            var sb = new StringBuilder();
            foreach (var column in columnKeys)
            {
                //The separator is what is conditional, not the predicate. The two lines were
                //transposed, so the buffer stayed empty on every pass and the statement went out
                //without a WHERE clause — that is, unfiltered.
                if (sb.Length > 0)
                    sb.Append(" AND ");

                sb.Append($"{column.NameFormatted}={formatter.FormatParameterName(column.Name)}");
            }
            if (sb.Length > 0)
                statement.Text += " WHERE " + sb.ToString();

            return statement;
        }

        public static TextStatement GetQueryByKeysStatement(Type type, IEnumerable<object> values, IDatabaseFormatter formatter)
        {
            var statement = GetQueryStatement(type, formatter);

            var columnKeys = statement.TableDescriptor.Columns.Where(c => c.ColumnAtribute.Key != KeyType.None);
            if (columnKeys.Count() != 1)
                throw new DatabaseException("Only one Key must be defined for QueryByKeys");

            var key = columnKeys.First();

            var placeholders = new StringBuilder();
            var index = 0;
            foreach (var value in values)
            {
                if (placeholders.Length > 0)
                    placeholders.Append(',');

                //The name is generated, never derived from the value, so a key cannot pose as an
                //identifier. The value travels beside the statement and is bound by whoever
                //executes it — it never becomes SQL text.
                var name = $"key{index++}";
                placeholders.Append(formatter.FormatParameterName(name));
                statement.Parameters.Add(new StatementParameter(name, value, key));
            }

            statement.Text += $" WHERE {key.NameFormatted} IN ({placeholders}) ";

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

using System.Data.Common;
using System.Text;
using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Exceptions;
using cl2j.Database.Helpers;
using Microsoft.Data.SqlClient;

namespace cl2j.Database.SqlServer
{
    internal sealed class SqlServerCommandBuilder(IIdentifierGenerator? identifierGenerator) : ICommandBuilder, IDatabaseFormatter
    {
        private readonly IIdentifierGenerator identifierGenerator = identifierGenerator ?? GuidIdentifierGenerator.Default;

        public bool Support(DbConnection connection)
        {
            return connection is SqlConnection;
        }

        public IDatabaseFormatter DatabaseFormatter => this;

        public IIdentifierGenerator IdentifierGenerator => identifierGenerator;

        public TextStatement GetTableExistsStatement(Type type)
        {
            var tableDescriptor = TableDescriptorFactory.Create(type, this);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"SELECT TOP 1 * FROM {tableDescriptor.NameFormatted}"
            };
        }

        public TextStatement GetDropTableStatement(Type type)
        {
            return CommandBuilderHelpers.GetDropTableStatement(type, this);
        }

        public TextStatement GetCreateTableStatement(Type type)
        {
            return CommandBuilderHelpers.GetCreateTableStatement(type, this);
        }

        public TextStatement GetInsertStatement(Type type)
        {
            return CommandBuilderHelpers.GetInsertStatement(type, this);
        }

        public TextStatement GetUpdateStatement(Type type)
        {
            return CommandBuilderHelpers.GetUpdateStatement(type, this);
        }

        public TextStatement GetUpdateColumnStatement(Type type, string columnName)
        {
            return CommandBuilderHelpers.GetUpdateColumnStatement(type, columnName, this);
        }

        public TextStatement GetDeleteStatement(Type type)
        {
            return CommandBuilderHelpers.GetDeleteStatement(type, this);
        }

        public TextStatement GetQueryStatement(Type type)
        {
            return CommandBuilderHelpers.GetQueryStatement(type, this);
        }

        public TextStatement GetQueryStatement(Type type, Type paramType)
        {
            return CommandBuilderHelpers.GetQueryStatement(type, paramType, this);
        }

        public TextStatement GetQueryByKeyStatement(Type type)
        {
            var statement = CommandBuilderHelpers.GetQueryStatement(type, this);

            var columnKeys = statement.TableDescriptor.Columns.Where(c => c.ColumnAtribute.Key != KeyType.None);
            var sb = new StringBuilder();
            foreach (var column in columnKeys)
            {
                //The separator is what is conditional, not the predicate. The two lines were
                //transposed, so the buffer stayed empty on every pass and the statement went out
                //without a WHERE clause — that is, unfiltered.
                if (sb.Length > 0)
                    sb.Append(" AND ");

                sb.Append($"{column.NameFormatted}={FormatParameterName(column.Name)}");
            }
            if (sb.Length > 0)
                statement.Text += " WHERE " + sb.ToString();

            return statement;
        }

        public TextStatement GetQueryByKeysStatement(Type type, IEnumerable<object> values)
        {
            var statement = CommandBuilderHelpers.GetQueryStatement(type, this);

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
                placeholders.Append(FormatParameterName(name));
                statement.Parameters.Add(new StatementParameter(name, value, key));
            }

            statement.Text += $" WHERE {key.NameFormatted} IN ({placeholders}) ";

            return statement;
        }


        public async Task BulkInsert<TIn>(DbConnection connection, IEnumerable<TIn> items, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            var sqlConnection = connection as SqlConnection ?? throw new DatabaseException($"SqlConnection required. '{connection.GetType().Name}' received.");

            var tableDescriptor = TableDescriptorFactory.Create(typeof(TIn), this);
            var columns = tableDescriptor.Columns.Where(c => c.ColumnAtribute.Key != KeyType.Key);

            using var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.Default, transaction as SqlTransaction);
            var dataTable = DataTableHelpers.CreateDataTable(items, tableDescriptor.NameFormatted, columns);

            bulkCopy.DestinationTableName = tableDescriptor.NameFormatted;

            foreach (var column in dataTable.Columns)
                bulkCopy.ColumnMappings.Add(column.ToString(), column.ToString());

            bulkCopy.BulkCopyTimeout = ConnectionExtensions.DatabaseOptions.BulkInsertTimeout?.Seconds ?? connection.ConnectionTimeout;
            await bulkCopy.WriteToServerAsync(dataTable);
        }

        #region IDatabaseFormatter

        public string FormatTableName(string table, string? schema = null)
        {
            if (schema is null)
                return "[" + table + "]";
            return $"[{schema}].[{table}]";
        }

        public string FormatColumnName(string name)
        {
            return "[" + name + "]";
        }

        public string GetColumnDataType(ColumnDescriptor column)
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
                //Nullable<T> is not the type it wraps, so every nullable value type used to miss
                //every branch below and land on the varchar(MAX) default: an ordinary "int?"
                //column was created as text. Unwrap first, once. See issue #27.
                var type = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

                if (type.IsEnum)
                    propertyTypeDesc = "int";
                else if (type == Types.TypeBool)
                    propertyTypeDesc = "bit";
                else if (type == Types.TypeByte)
                    propertyTypeDesc = "tinyint";
                else if (type == Types.TypeShort)
                    propertyTypeDesc = "smallint";
                else if (type == Types.TypeInt)
                    propertyTypeDesc = "int";
                //long used to share the int branch, so anything past int.MaxValue overflowed the
                //column this library had created for it.
                else if (type == Types.TypeLong)
                    propertyTypeDesc = "bigint";
                //double and float used to share the decimal branch. With no Length that landed on
                //bare decimal, which SQL Server reads as decimal(18,0) — so 1.5 was stored as 2.
                //These are floating-point values and the server has floating-point types for them.
                else if (type == Types.TypeDouble)
                    propertyTypeDesc = "float";
                else if (type == Types.TypeFloat)
                    propertyTypeDesc = "real";
                else if (type == Types.TypeDecimal)
                    propertyTypeDesc = FormatDecimal(columnAttr);
                //GetColumnKeyType always knew about Guid; this did not, so a Guid that was not a
                //key fell through to the default and was stored as text.
                else if (type == Types.TypeGuid)
                    propertyTypeDesc = "uniqueidentifier";
                else if (type == Types.TypeString)
                    propertyTypeDesc = columnAttr.Length > 0 ? $"varchar({columnAttr.Length})" : "varchar(max)";
                else if (type == Types.TypeByteArray)
                    propertyTypeDesc = columnAttr.Length > 0 ? $"varbinary({columnAttr.Length})" : "varbinary(max)";
                else if (type == Types.TypeDateTimeOffset)
                    propertyTypeDesc = "datetimeoffset";
                else if (type == Types.TypeDateTime)
                    propertyTypeDesc = "datetime2";
                else if (type == Types.TypeDateOnly)
                    propertyTypeDesc = "date";
                else if (type == Types.TypeTimeOnly || type == Types.TypeTimeSpan)
                    propertyTypeDesc = "time";
                else if (type == Types.TypeChar)
                    propertyTypeDesc = "char(1)";
                //No silent fallback. A varchar(MAX) default is how a Guid came to be stored as
                //text without anyone noticing, and how every nullable column did the same. A type
                //nobody mapped is a failure at schema creation, which is the cheapest moment to
                //find it; TypeName remains the way to say what the column should be.
                else
                    throw new DatabaseException($"No column type for '{type.Name}' on '{column.Name}'. Declare one with [Column(TypeName = \"...\")].");
            }

            if (!string.IsNullOrEmpty(columnAttr.Default))
                propertyTypeDesc += $" DEFAULT {columnAttr.Default}";

            return propertyTypeDesc;
        }


        /// <summary>
        ///     A decimal needs its precision declared. Without <c>Length</c> the column would be
        ///     bare <c>decimal</c>, which SQL Server reads as <c>decimal(18,0)</c> — an amount of
        ///     12.34 stored as 12, rounded on the way in with nothing to say so. Refusing is louder
        ///     than guessing a precision the caller did not choose. See issue #27.
        /// </summary>
        private static string FormatDecimal(ColumnAttribute columnAttr)
        {
            if (columnAttr.Length <= 0)
                throw new DatabaseException("A decimal column needs its precision: [Column(Length = 18, Decimals = 2)].");

            return $"decimal({columnAttr.Length},{columnAttr.Decimals})";
        }

        public string GetColumnKeyType(ColumnDescriptor column)
        {
            if (column.Property.PropertyType == Types.TypeInt)
                return " IDENTITY(1,1) PRIMARY KEY";
            else if (column.Property.PropertyType == Types.TypeString)
                return " NOT NULL";
            else if (column.Property.PropertyType == Types.TypeGuid)
                return " UNIQUEIDENTIFIER DEFAULT NEWID() PRIMARY KEY";

            throw new DatabaseException($"Unsupported key type '{column.Property.PropertyType.Name}'");
        }

        public string FormatParameterName(string name)
        {
            return "@" + name;
        }

        #endregion
    }
}

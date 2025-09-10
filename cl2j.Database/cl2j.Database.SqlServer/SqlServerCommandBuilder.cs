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
    internal class SqlServerCommandBuilder(IIdentifierGenerator? identifierGenerator) : ICommandBuilder, IDatabaseFormatter
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
                if (sb.Length > 0)
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
            var inClause = GenerateInClause(values);
            statement.Text += $" WHERE {key.NameFormatted} IN ({inClause}) ";

            return statement;
        }

        private string GenerateInClause(IEnumerable<object> values)
        {
            var sb = new StringBuilder();
            foreach (var value in values)
            {
                if (sb.Length > 0) sb.Append(',');

                var formattedValue = DatabaseFormatter.FormatParameterValue(value);
                sb.Append(formattedValue);
            }
            return sb.ToString();
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
                if (propertyInfo.PropertyType.IsEnum)
                    propertyTypeDesc = "int";
                else if (propertyInfo.PropertyType == Types.TypeBool)
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

        public string FormatParameterValue(object value)
        {
            if (value.GetType() == typeof(string))
                return $"'{value}'";
            return value?.ToString() ?? string.Empty;
        }

        #endregion
    }
}

using System.Data.Common;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Exceptions;
using cl2j.Database.Helpers;
using Microsoft.Data.SqlClient;

namespace cl2j.Database.CommandBuilders
{
    public class SqlServerCommandBuilder : ICommandBuilder, IDatabaseFormatter
    {
        public bool Support(DbConnection connection)
        {
            return connection is SqlConnection;
        }

        public IDatabaseFormatter DatabaseFormatter => this;

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
            var tableDescriptor = TableDescriptorFactory.Create(type, this);

            return new TextStatement
            {
                TableDescriptor = tableDescriptor,
                Text = $"DROP TABLE {tableDescriptor.NameFormatted}"
            };
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

        public async Task InsertBatch<TIn>(DbConnection connection, IEnumerable<TIn> items, CancellationToken cancellationToken, DbTransaction? transaction = null)
        {
            var sqlConnection = connection as SqlConnection ?? throw new DatabaseException($"SqlConnection is required. '{connection.GetType().Name}' received.");

            var type = typeof(TIn);
            var tableDescriptor = TableDescriptorFactory.Create(type, this);
            var columns = tableDescriptor.Columns.Where(c => c.ColumnAtribute.Key != KeyType.Key);

            using (var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.Default, transaction as SqlTransaction))
            {
                var dataTable = DataTableHelpers.CreateDataTable(items, tableDescriptor.NameFormatted, columns);

                bulkCopy.DestinationTableName = tableDescriptor.NameFormatted;

                foreach (var column in dataTable.Columns)
                    bulkCopy.ColumnMappings.Add(column.ToString(), column.ToString());

                bulkCopy.BulkCopyTimeout = connection.ConnectionTimeout;
                await bulkCopy.WriteToServerAsync(dataTable);
            }
        }

        #region IDatabaseFormatter

        public string FormatTableName(TableMetaData tableMetaData)
        {
            return FormatTableName(tableMetaData.Table, tableMetaData.Schema);
        }

        public string FormatTableName(string table, string? schema = null)
        {
            if (schema is null)
                return "[" + table + "]";
            return $"[{schema}].[{table}]";
        }

        public string FormatColumnName(string column)
        {
            return "[" + column + "]";
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

        public string FormatParameterName(string column)
        {
            return "@" + column;
        }

        #endregion
    }
}

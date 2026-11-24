using System.Data;
using System.Data.Common;
using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Exceptions;
using cl2j.Database.Helpers;
using Microsoft.Data.SqlClient;

namespace cl2j.Database.SqlServer
{
    internal sealed class SqlServerCommandBuilder(IIdentifierGenerator? identifierGenerator) : RelationalCommandBuilder(identifierGenerator)
    {
        public override bool Support(DbConnection connection)
        {
            return connection is SqlConnection;
        }

        //dbo is what SQL Server puts a table in when the type names no schema.
        protected override string? DefaultSchemaName => "dbo";

        protected override string GetIdentityRetrieval(TableDescriptor table, ColumnDescriptor identity)
        {
            //SCOPE_IDENTITY returns numeric(38,0); cast it back to the type the caller declared
            //so ExecuteScalar hands over an int rather than a decimal.
            var cast = identity.Property.PropertyType == Types.TypeLong ? "bigint" : "int";
            return $";SELECT CAST(SCOPE_IDENTITY() AS {cast})";
        }

        protected override async Task WriteBulkAsync(DbConnection connection, TableDescriptor table, IReadOnlyList<ColumnDescriptor> columns, DataTable rows, CancellationToken cancellationToken, DbTransaction? transaction)
        {
            var sqlConnection = connection as SqlConnection ?? throw new DatabaseException($"SqlConnection required. '{connection.GetType().Name}' received.");

            using var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.Default, transaction as SqlTransaction);

            bulkCopy.DestinationTableName = table.NameFormatted;

            foreach (var column in rows.Columns)
                bulkCopy.ColumnMappings.Add(column.ToString(), column.ToString());

            bulkCopy.BulkCopyTimeout = ConnectionExtensions.DatabaseOptions.GetBulkInsertTimeoutSeconds(connection.ConnectionTimeout);
            await bulkCopy.WriteToServerAsync(rows, cancellationToken);
        }

        protected override string QuoteIdentifier(string name)
        {
            return "[" + name + "]";
        }

        protected override string GetJsonColumnType(ColumnAttribute attribute)
        {
            var length = attribute.Length <= 0 ? "max" : attribute.Length.ToString();
            return $"varchar({length})";
        }

        protected override string? MapClrType(Type type, ColumnDescriptor column)
        {
            var columnAttr = column.ColumnAtribute;

            if (type.IsEnum)
                return "int";
            if (type == Types.TypeBool)
                return "bit";
            if (type == Types.TypeByte)
                return "tinyint";
            if (type == Types.TypeShort)
                return "smallint";
            if (type == Types.TypeInt)
                return "int";
            //long used to share the int branch, so anything past int.MaxValue overflowed the
            //column this library had created for it.
            if (type == Types.TypeLong)
                return "bigint";
            //double and float used to share the decimal branch. With no Length that landed on
            //bare decimal, which SQL Server reads as decimal(18,0) — so 1.5 was stored as 2.
            //These are floating-point values and the server has floating-point types for them.
            if (type == Types.TypeDouble)
                return "float";
            if (type == Types.TypeFloat)
                return "real";
            if (type == Types.TypeDecimal)
                return FormatDecimal(columnAttr);
            //GetColumnKeyType always knew about Guid; this did not, so a Guid that was not a
            //key fell through to the default and was stored as text.
            if (type == Types.TypeGuid)
                return "uniqueidentifier";
            if (type == Types.TypeString)
                return columnAttr.Length > 0 ? $"varchar({columnAttr.Length})" : "varchar(max)";
            if (type == Types.TypeByteArray)
                return columnAttr.Length > 0 ? $"varbinary({columnAttr.Length})" : "varbinary(max)";
            if (type == Types.TypeDateTimeOffset)
                return "datetimeoffset";
            if (type == Types.TypeDateTime)
                return "datetime2";
            if (type == Types.TypeDateOnly)
                return "date";
            if (type == Types.TypeTimeOnly || type == Types.TypeTimeSpan)
                return "time";
            if (type == Types.TypeChar)
                return "char(1)";

            return null;
        }

        protected override string IdentityKeyDeclaration => " IDENTITY(1,1) PRIMARY KEY";

        protected override string GuidKeyDeclaration => " DEFAULT NEWID() PRIMARY KEY";
    }
}

using System.Data;
using System.Data.Common;
using System.Text;
using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Exceptions;
using cl2j.Database.Helpers;
using MySqlConnector;

namespace cl2j.Database.MySql
{
    /// <summary>
    ///     The MySQL command builder, on <see cref="RelationalCommandBuilder"/>.
    ///
    ///     <para>
    ///     Requires MySQL 8.0.13 or later: a Guid key is declared <c>DEFAULT (UUID())</c>, and
    ///     expression defaults arrived in 8.0.13.
    ///     </para>
    ///
    ///     <para>
    ///     Bulk insert has two strategies, chosen by the connection string and both all-or-nothing:
    ///     <see cref="MySqlBulkCopy"/> (<c>LOAD DATA LOCAL INFILE</c>) when it says
    ///     <c>AllowLoadLocalInfile=true</c>, otherwise multi-row <c>INSERT</c> statements. See
    ///     <see cref="WriteBulkAsync"/>.
    ///     </para>
    /// </summary>
    internal sealed class MySqlCommandBuilder(IIdentifierGenerator? identifierGenerator) : RelationalCommandBuilder(identifierGenerator)
    {
        /// <summary>
        ///     Rows per statement when bulk insert falls back to a multi-row INSERT. MySqlConnector
        ///     substitutes parameters client-side, so the bound is <c>max_allowed_packet</c> (64 MB by
        ///     default), not a parameter count.
        /// </summary>
        internal const int InsertBatchRows = 1000;

        public override bool Support(DbConnection connection)
        {
            return connection is MySqlConnection;
        }

        //MySQL calls a database what SQL Server calls a schema: a type that names no schema lives in
        //the database of the connection.
        protected override string CurrentSchemaExpression => "DATABASE()";

        protected override string QuoteIdentifier(string name)
        {
            //A backtick inside a name is escaped by doubling it.
            return "`" + name.Replace("`", "``", StringComparison.Ordinal) + "`";
        }

        //LAST_INSERT_ID() is a BIGINT UNSIGNED, which MySqlConnector hands back as a ulong; the core
        //converts the identity to the type the caller asked for.
        protected override string GetIdentityRetrieval(TableDescriptor table, ColumnDescriptor identity)
        {
            return ";SELECT LAST_INSERT_ID()";
        }

        protected override string IdentityKeyDeclaration => " AUTO_INCREMENT PRIMARY KEY";

        protected override string GuidKeyDeclaration => " DEFAULT (UUID()) PRIMARY KEY";

        //A JSON property without a length is the native json type, which validates on write and
        //normalises the text (key order, whitespace).
        protected override string GetJsonColumnType(ColumnAttribute attribute)
        {
            return attribute.Length <= 0 ? "json" : $"varchar({attribute.Length})";
        }

        /// <summary>
        ///     The SQL Server mapping, translated. The differences that matter to a caller:
        ///     <list type="bullet">
        ///         <item><c>string</c> without a length is <c>longtext</c>: MySQL has no
        ///         <c>varchar(max)</c>. A <c>varchar(n)</c> counts against the 65,535-byte row limit
        ///         at four bytes per character in utf8mb4.</item>
        ///         <item><c>DateTimeOffset</c> is <c>datetime(6)</c> holding UTC: MySQL has no type
        ///         that keeps the offset.</item>
        ///         <item><c>Guid</c> is <c>char(36)</c>, the form MySqlConnector reads back as a
        ///         Guid by default.</item>
        ///         <item>Temporal types carry six fractional digits; .NET has seven.</item>
        ///     </list>
        /// </summary>
        protected override string? MapClrType(Type type, ColumnDescriptor column)
        {
            var columnAttr = column.ColumnAtribute;

            if (type.IsEnum)
                return "int";
            if (type == Types.TypeBool)
                return "tinyint(1)";
            //Signed tinyint reads back as sbyte, which would not unbox to byte.
            if (type == Types.TypeByte)
                return "tinyint unsigned";
            if (type == Types.TypeShort)
                return "smallint";
            if (type == Types.TypeInt)
                return "int";
            if (type == Types.TypeLong)
                return "bigint";
            if (type == Types.TypeDouble)
                return "double";
            if (type == Types.TypeFloat)
                return "float";
            if (type == Types.TypeDecimal)
                return FormatDecimal(columnAttr);
            if (type == Types.TypeGuid)
                return "char(36)";
            if (type == Types.TypeString)
                return columnAttr.Length > 0 ? $"varchar({columnAttr.Length})" : "longtext";
            if (type == Types.TypeByteArray)
                return columnAttr.Length > 0 ? $"varbinary({columnAttr.Length})" : "longblob";
            if (type == Types.TypeDateTimeOffset || type == Types.TypeDateTime)
                return "datetime(6)";
            if (type == Types.TypeDateOnly)
                return "date";
            if (type == Types.TypeTimeOnly || type == Types.TypeTimeSpan)
                return "time(6)";
            if (type == Types.TypeChar)
                return "char(1)";

            return null;
        }

        /// <summary>
        ///     Two strategies, chosen by the connection string, and the same outcome from both: the
        ///     rows are all written or none is. Nothing is written for an empty input.
        ///
        ///     <para>
        ///     With <c>AllowLoadLocalInfile=true</c>, <see cref="MySqlBulkCopy"/>: it streams the
        ///     rows through <c>LOAD DATA LOCAL INFILE</c>, the fast path. It needs the server to
        ///     allow it too (<c>local_infile=ON</c>, off by default since MySQL 8.0). <c>LOCAL</c>
        ///     makes the server treat a duplicate key as <c>IGNORE</c>: the row is skipped with a
        ///     warning rather than failing the load. <c>MySqlBulkCopy</c> then throws on the row
        ///     count.
        ///     </para>
        ///
        ///     <para>
        ///     Otherwise, multi-row <c>INSERT</c> statements of <see cref="InsertBatchRows"/> rows:
        ///     slower, but it needs no server setting, and a duplicate fails the statement.
        ///     </para>
        ///
        ///     <para>
        ///     Neither is atomic on its own (the load keeps the rows around a skipped duplicate, and
        ///     each INSERT statement commits by itself). When the caller passes no transaction this
        ///     method opens one and rolls it back on failure, which gives MySQL the all-or-nothing
        ///     behaviour of <c>SqlBulkCopy</c>. A caller's transaction is used as is.
        ///     </para>
        /// </summary>
        protected override async Task WriteBulkAsync(DbConnection connection, TableDescriptor table, IReadOnlyList<ColumnDescriptor> columns, DataTable rows, CancellationToken cancellationToken, DbTransaction? transaction)
        {
            var mySqlConnection = connection as MySqlConnection ?? throw new DatabaseException($"MySqlConnection required. '{connection.GetType().Name}' received.");

            if (rows.Rows.Count == 0)
                return;

            if (transaction is not null)
            {
                await WriteRowsAsync(mySqlConnection, table, columns, rows, transaction as MySqlTransaction, cancellationToken);
                return;
            }

            //Disposing a transaction that was not committed rolls it back.
            await using var own = await mySqlConnection.BeginTransactionAsync(cancellationToken);
            await WriteRowsAsync(mySqlConnection, table, columns, rows, own, cancellationToken);
            await own.CommitAsync(cancellationToken);
        }

        private static Task WriteRowsAsync(MySqlConnection connection, TableDescriptor table, IReadOnlyList<ColumnDescriptor> columns, DataTable rows, MySqlTransaction? transaction, CancellationToken cancellationToken)
        {
            var builder = new MySqlConnectionStringBuilder(connection.ConnectionString);
            return builder.AllowLoadLocalInfile
                ? BulkCopyAsync(connection, transaction, table, columns, rows, cancellationToken)
                : BatchedInsertAsync(connection, transaction, table, columns, rows, cancellationToken);
        }

        private static async Task BulkCopyAsync(MySqlConnection connection, MySqlTransaction? transaction, TableDescriptor table, IReadOnlyList<ColumnDescriptor> columns, DataTable rows, CancellationToken cancellationToken)
        {
            var bulkCopy = new MySqlBulkCopy(connection, transaction)
            {
                DestinationTableName = table.NameFormatted,
                BulkCopyTimeout = ConnectionExtensions.DatabaseOptions.GetBulkInsertTimeoutSeconds(connection.ConnectionTimeout),
            };

            //By name: without mappings MySqlBulkCopy maps by ordinal against the table, which is off
            //by one as soon as an AUTO_INCREMENT key is left out. The destination name is the
            //unquoted column name.
            foreach (var column in columns)
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(rows.Columns[column.Name]!.Ordinal, column.Name));

            //MySqlBulkCopy compares the rows sent with the rows inserted and throws on a mismatch,
            //so a skipped duplicate is not silent.
            await bulkCopy.WriteToServerAsync(rows, cancellationToken);
        }

        private static async Task BatchedInsertAsync(MySqlConnection connection, MySqlTransaction? transaction, TableDescriptor table, IReadOnlyList<ColumnDescriptor> columns, DataTable rows, CancellationToken cancellationToken)
        {
            var head = $"INSERT INTO {table.NameFormatted} ({string.Join(',', columns.Select(c => c.NameFormatted))}) VALUES ";
            var ordinals = columns.Select(c => rows.Columns[c.Name]!.Ordinal).ToArray();
            var timeout = ConnectionExtensions.DatabaseOptions.GetBulkInsertTimeoutSeconds(connection.ConnectionTimeout);

            for (var start = 0; start < rows.Rows.Count; start += InsertBatchRows)
            {
                var end = Math.Min(start + InsertBatchRows, rows.Rows.Count);

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandTimeout = timeout;

                var sb = new StringBuilder(head);
                for (var r = start; r < end; r++)
                {
                    if (r > start)
                        sb.Append(',');
                    sb.Append('(');
                    for (var c = 0; c < ordinals.Length; c++)
                    {
                        if (c > 0)
                            sb.Append(',');
                        var name = $"r{r - start}c{c}";
                        sb.Append('@').Append(name);
                        command.Parameters.AddWithValue(name, rows.Rows[r][ordinals[c]]);
                    }
                    sb.Append(')');
                }

                command.CommandText = sb.ToString();
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }
}

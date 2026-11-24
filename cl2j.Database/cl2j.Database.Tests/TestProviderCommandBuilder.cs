using System.Data;
using System.Data.Common;
using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Helpers;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     The smallest provider that derives from <see cref="RelationalCommandBuilder"/>: it quotes
    ///     with backticks, reads the identity back with <c>;SELECT ID()</c> and records what its hooks
    ///     are given. Nothing in it is meant to look like a real database; it is distinguishable from
    ///     SQL Server on every axis the base class lets a provider vary.
    /// </summary>
    internal sealed class TestProviderCommandBuilder(string? defaultSchemaName = null, bool providesCurrentSchema = true, int maxKeysPerStatement = TestProviderCommandBuilder.KeysPerStatement) : RelationalCommandBuilder(null)
    {
        private readonly string? defaultSchemaName = defaultSchemaName;
        private readonly bool providesCurrentSchema = providesCurrentSchema;

        /// <summary>The batch size the test provider declares.</summary>
        internal const int KeysPerStatement = 3;

        /// <summary>Every parameter <see cref="PrepareParameter"/> was called with, in order.</summary>
        public List<(DbParameter Parameter, ColumnDescriptor? Column)> Prepared { get; } = [];

        /// <summary>The value each prepared parameter carried at the moment the provider was shown it.</summary>
        public List<object?> ValuesAtPreparation { get; } = [];

        /// <summary>What the last <see cref="WriteBulkAsync"/> was handed.</summary>
        public BulkCall? LastBulk { get; private set; }

        public override int MaxKeysPerStatement => maxKeysPerStatement;

        public override bool Support(DbConnection connection) => connection is RecordingDbConnection;

        public override void PrepareParameter(DbParameter parameter, ColumnDescriptor? column)
        {
            Prepared.Add((parameter, column));
            ValuesAtPreparation.Add(parameter.Value);
        }

        protected override string QuoteIdentifier(string name) => "`" + name + "`";

        protected override string? DefaultSchemaName => defaultSchemaName;

        protected override string CurrentSchemaExpression => providesCurrentSchema ? "SCHEMA()" : base.CurrentSchemaExpression;

        protected override string GetIdentityRetrieval(TableDescriptor table, ColumnDescriptor identity)
            => ";SELECT ID()";

        protected override string GetJsonColumnType(ColumnAttribute attribute)
            => attribute.Length <= 0 ? "JSON" : $"VARCHAR({attribute.Length})";

        protected override string? MapClrType(Type type, ColumnDescriptor column)
        {
            if (type.IsEnum || type == Types.TypeInt)
                return "INT";
            if (type == Types.TypeLong)
                return "BIGINT";
            if (type == Types.TypeBool)
                return "BOOL";
            if (type == Types.TypeGuid)
                return "CHAR(36)";
            if (type == Types.TypeString)
                return column.ColumnAtribute.Length > 0 ? $"VARCHAR({column.ColumnAtribute.Length})" : "TEXT";
            if (type == Types.TypeDecimal)
                return FormatDecimal(column.ColumnAtribute);

            return null;
        }

        protected override string IdentityKeyDeclaration => " AUTO PK";

        protected override string GuidKeyDeclaration => " DEFAULT UUID() PK";

        protected override Task WriteBulkAsync(DbConnection connection, TableDescriptor table, IReadOnlyList<ColumnDescriptor> columns, DataTable rows, CancellationToken cancellationToken, DbTransaction? transaction)
        {
            LastBulk = new BulkCall(connection, table, columns, rows, cancellationToken, transaction);
            return Task.CompletedTask;
        }

        internal sealed record BulkCall(DbConnection Connection, TableDescriptor Table, IReadOnlyList<ColumnDescriptor> Columns, DataTable Rows, CancellationToken CancellationToken, DbTransaction? Transaction);
    }
}

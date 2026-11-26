using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using cl2j.Database.CommandBuilders;
using cl2j.Database.Descriptors;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     <see cref="ICommandBuilder"/> gives a provider two optional members a default: how many
    ///     keys one query-by-keys statement may carry, and a hook to adjust a parameter. A builder
    ///     written straight against the interface, rather than on <c>RelationalCommandBuilder</c>,
    ///     gets exactly these.
    /// </summary>
    public class CommandBuilderDefaultsTests
    {
        /// <summary>Implements the interface and nothing else, so every default stays in place.</summary>
        private sealed class BareCommandBuilder : ICommandBuilder
        {
            public bool Support(DbConnection connection) => throw new NotSupportedException();

            public IDatabaseFormatter DatabaseFormatter => throw new NotSupportedException();

            public IIdentifierGenerator IdentifierGenerator => throw new NotSupportedException();

            public TextStatement GetTableExistsStatement(Type type) => throw new NotSupportedException();

            public TextStatement GetDropTableStatement(Type type) => throw new NotSupportedException();

            public TextStatement GetCreateTableStatement(Type type) => throw new NotSupportedException();

            public TextStatement GetInsertStatement(Type type) => throw new NotSupportedException();

            public TextStatement GetUpdateStatement(Type type) => throw new NotSupportedException();

            public TextStatement GetUpdateColumnStatement(Type type, string columnName) => throw new NotSupportedException();

            public TextStatement GetDeleteStatement(Type type) => throw new NotSupportedException();

            public TextStatement GetQueryStatement(Type type) => throw new NotSupportedException();

            public TextStatement GetQueryStatement(Type type, Type paramType) => throw new NotSupportedException();

            public TextStatement GetQueryByKeyStatement(Type type) => throw new NotSupportedException();

            public TextStatement GetQueryByKeysStatement(Type type, IEnumerable<object> values) => throw new NotSupportedException();

            public Task BulkInsert<TIn>(DbConnection connection, IEnumerable<TIn> items, CancellationToken cancellationToken, DbTransaction? transaction = null) => throw new NotSupportedException();
        }

        [Fact]
        public void The_default_is_the_library_wide_limit_of_keys_per_statement()
        {
            ICommandBuilder builder = new BareCommandBuilder();

            Assert.Equal(QueryKeyBatches.MaxKeysPerStatement, builder.MaxKeysPerStatement);
            Assert.Equal(2000, builder.MaxKeysPerStatement);
        }

        [Fact]
        public void The_default_hook_leaves_the_parameter_as_it_was()
        {
            ICommandBuilder builder = new BareCommandBuilder();
            var parameter = new TestDbParameter { ParameterName = "@p", Value = "x" };

            builder.PrepareParameter(parameter, column: null);

            Assert.Equal("@p", parameter.ParameterName);
            Assert.Equal("x", parameter.Value);
        }

        private sealed class TestDbParameter : DbParameter
        {
            public override System.Data.DbType DbType { get; set; }

            public override System.Data.ParameterDirection Direction { get; set; }

            public override bool IsNullable { get; set; }

            [AllowNull]
            public override string ParameterName { get; set; } = string.Empty;

            [AllowNull]
            public override string SourceColumn { get; set; } = string.Empty;

            [AllowNull]
            public override object Value { get; set; } = string.Empty;

            public override bool SourceColumnNullMapping { get; set; }

            public override int Size { get; set; }

            public override void ResetDbType() { }
        }
    }
}

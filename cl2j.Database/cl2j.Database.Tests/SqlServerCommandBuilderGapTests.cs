using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Exceptions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     The corners of the SQL Server builder the statement tests do not reach: keys that are not
    ///     plain identities, a composite key on a multi-key read, a Guid key, a char column, and the
    ///     two ways bulk insert fails without a server.
    /// </summary>
    public class SqlServerCommandBuilderGapTests
    {
        [Table("SelfKeyed")]
        private sealed class SelfKeyedRow
        {
            [Column(Name = "Id", Key = KeyType.SelfGeneratedKey)]
            public int Id { get; set; }

            [Column(Name = "Label", Length = 20)]
            public string Label { get; set; } = string.Empty;
        }

        [Table("BigKeyed")]
        private sealed class BigKeyedRow
        {
            [Column(Name = "Id", Key = KeyType.Key)]
            public long Id { get; set; }

            [Column(Name = "Label", Length = 20)]
            public string Label { get; set; } = string.Empty;
        }

        [Table("GuidKeyed")]
        private sealed class GuidKeyedRow
        {
            [Column(Name = "Id", Key = KeyType.Key)]
            public Guid Id { get; set; }

            [Column(Name = "Label", Length = 20)]
            public string Label { get; set; } = string.Empty;
        }

        [Table("CharRow")]
        private sealed class CharRow
        {
            [Column(Name = "Id", Key = KeyType.Key, Length = 10)]
            public string Id { get; set; } = string.Empty;

            [Column(Name = "Grade")]
            public char Grade { get; set; }
        }

        [Table("DoubleKeyed")]
        private sealed class DoubleKeyedRow
        {
            [Column(Name = "Id", Key = KeyType.Key)]
            public double Id { get; set; }
        }

        private static ICommandBuilder Builder()
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            return CommandBuilderFactory.GetCommandBuilder(new SqlConnection());
        }

        private static string Normalise(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

        [Fact]
        public void Insert_asks_for_a_long_identity_back_as_bigint()
        {
            var statement = Builder().GetInsertStatement(typeof(BigKeyedRow));

            Assert.EndsWith(";SELECT CAST(SCOPE_IDENTITY() AS bigint)", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Insert_does_not_ask_for_an_identity_when_the_key_is_not_a_plain_key()
        {
            //A self-generated key is supplied by the caller, so there is no identity to read back.
            var statement = Builder().GetInsertStatement(typeof(SelfKeyedRow));

            Assert.DoesNotContain("SCOPE_IDENTITY", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Insert_does_not_ask_for_an_identity_when_the_key_is_a_guid()
        {
            //The server fills a Guid key from DEFAULT NEWID(), which SCOPE_IDENTITY does not report.
            var statement = Builder().GetInsertStatement(typeof(GuidKeyedRow));

            Assert.DoesNotContain("SCOPE_IDENTITY", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void QueryByKeys_refuses_a_composite_key()
        {
            var ex = Assert.Throws<DatabaseException>(() => Builder().GetQueryByKeysStatement(typeof(TenantScopedRow), ["a", "b"]));

            Assert.Equal("Only one Key must be defined for QueryByKeys", ex.Message);
        }

        [Fact]
        public void A_guid_key_is_created_with_a_server_side_default()
        {
            var statement = Builder().GetCreateTableStatement(typeof(GuidKeyedRow));

            //The type is written once, by the data type; the key part only adds the default.
            Assert.Contains("uniqueidentifier DEFAULT NEWID() PRIMARY KEY", Normalise(statement.Text), StringComparison.Ordinal);
            Assert.DoesNotContain("UNIQUEIDENTIFIER", Normalise(statement.Text), StringComparison.Ordinal);
        }

        [Fact]
        public void A_key_of_an_unsupported_type_is_refused_when_the_table_is_created()
        {
            var ex = Assert.Throws<DatabaseException>(() => Builder().GetCreateTableStatement(typeof(DoubleKeyedRow)));

            Assert.Equal("Unsupported key type 'Double'", ex.Message);
        }

        [Fact]
        public void A_char_column_is_a_single_character()
        {
            var formatter = Builder().DatabaseFormatter;
            var column = TableDescriptorFactory.Create(typeof(CharRow), formatter).Columns.Single(c => c.Name == "Grade");

            Assert.Equal("char(1)", formatter.GetColumnDataType(column));
        }

        [Fact]
        public async Task BulkInsert_refuses_a_connection_that_is_not_a_SqlConnection()
        {
            using var connection = new UnsupportedConnection();

            var ex = await Assert.ThrowsAsync<DatabaseException>(
                () => Builder().BulkInsert(connection, new[] { new Customer { Id = "a", Name = "A" } }, CancellationToken.None));

            Assert.Equal("SqlConnection required. 'UnsupportedConnection' received.", ex.Message);
        }

        [Fact]
        public async Task BulkInsert_fails_on_a_closed_connection_once_the_copy_is_prepared()
        {
            //No server needed: the table is built and mapped before the copy starts, and the copy
            //itself is what refuses a connection that was never opened.
            using var connection = new SqlConnection();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => Builder().BulkInsert(connection, new[] { new Customer { Id = "a", Name = "A" } }, CancellationToken.None));
        }
    }
}

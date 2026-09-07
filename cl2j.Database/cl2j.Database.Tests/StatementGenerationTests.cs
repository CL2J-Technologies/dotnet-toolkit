using cl2j.Database.CommandBuilders;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     The statements this package produces are text, and wrong text is still valid text. Both
    ///     defects found in September 2026 lived here and neither threw: one inlined key values
    ///     into the SQL, the other emitted no WHERE clause at all and returned the first row of the
    ///     table for any key. Asserting the exact text is the only thing that notices.
    /// </summary>
    public class StatementGenerationTests
    {
        private static ICommandBuilder Builder()
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            return CommandBuilderFactory.GetCommandBuilder(new SqlConnection());
        }

        //CREATE TABLE is built with AppendLine, so its line endings follow the machine. Normalise,
        //or the test passes on Windows and fails on the Linux runner.
        private static string Normalise(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

        [Fact]
        public void Insert_carries_a_string_key_like_any_other_column()
        {
            //Nothing generates a string key, so leaving it out of the insert sent NULL into a NOT
            //NULL primary key. See issue #26.
            var statement = Builder().GetInsertStatement(typeof(Customer));

            Assert.Equal("INSERT INTO [Customer] ([Id],[Name]) VALUES (@Id,@Name)", statement.Text);
        }

        [Fact]
        public void Insert_leaves_out_an_int_key_because_the_server_supplies_it()
        {
            //The DDL declares an int key IDENTITY(1,1); sending a value would be an error. And
            //since the server picks it, the statement asks for it back.
            var statement = Builder().GetInsertStatement(typeof(Counter));

            Assert.Equal(
                "INSERT INTO [Counter] ([Label]) VALUES (@Label);SELECT CAST(SCOPE_IDENTITY() AS int)",
                statement.Text);
        }

        [Fact]
        public void Insert_does_not_ask_for_an_identity_that_does_not_exist()
        {
            //A string key is carried by the insert, so there is nothing for the server to hand
            //back and SCOPE_IDENTITY would be null.
            var statement = Builder().GetInsertStatement(typeof(Customer));

            Assert.DoesNotContain("SCOPE_IDENTITY", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Update_sets_the_non_key_columns_and_filters_on_the_key()
        {
            var statement = Builder().GetUpdateStatement(typeof(Customer));

            Assert.Equal("UPDATE [Customer] SET [Name]=@Name WHERE [Id]=@Id", statement.Text);
        }

        [Fact]
        public void Update_column_sets_only_that_column()
        {
            var statement = Builder().GetUpdateColumnStatement(typeof(Customer), "Name");

            Assert.Equal("UPDATE [Customer] SET [Name]=@Name WHERE [Id]=@Id", statement.Text);
        }

        [Fact]
        public void Update_column_refuses_a_column_that_does_not_exist()
        {
            var builder = Builder();

            Assert.Throws<ArgumentException>(() => builder.GetUpdateColumnStatement(typeof(Customer), "Nope"));
        }

        [Fact]
        public void Update_column_refuses_a_key_column()
        {
            //Updating a key in place would move the row out from under its own WHERE clause.
            var builder = Builder();

            Assert.Throws<ArgumentException>(() => builder.GetUpdateColumnStatement(typeof(Customer), "Id"));
        }

        [Fact]
        public void Delete_filters_on_the_key()
        {
            var statement = Builder().GetDeleteStatement(typeof(Customer));

            Assert.Equal("DELETE FROM [Customer] WHERE [Id]=@Id", statement.Text);
        }

        [Fact]
        public void A_composite_key_joins_its_columns_with_AND()
        {
            var statement = Builder().GetDeleteStatement(typeof(TenantScopedRow));

            Assert.Equal("DELETE FROM [TenantScopedRow] WHERE [TenantId]=@TenantId AND [Id]=@Id", statement.Text);
        }

        [Fact]
        public void Query_selects_every_column()
        {
            var statement = Builder().GetQueryStatement(typeof(Customer));

            Assert.Equal("SELECT [Id],[Name] FROM [Customer]", statement.Text);
        }

        [Fact]
        public void Query_with_a_parameter_type_filters_on_that_type_columns()
        {
            var statement = Builder().GetQueryStatement(typeof(Customer), typeof(CustomerFilter));

            Assert.Equal("SELECT [Id],[Name] FROM [Customer] WHERE [Name]=@Name", statement.Text);
        }

        [Fact]
        public void Drop_table_qualifies_the_schema_when_there_is_one()
        {
            var statement = Builder().GetDropTableStatement(typeof(Invoice));

            Assert.Equal("DROP TABLE [billing].[Invoice]", statement.Text);
        }

        [Fact]
        public void Table_exists_asks_the_catalog_rather_than_the_table()
        {
            //It used to be SELECT TOP 1 * against the table, with the caller reading any exception
            //as "no". A dropped connection or a missing SELECT permission therefore answered "the
            //table does not exist", and CreateTableIfRequired went on to create one that was there.
            var statement = Builder().GetTableExistsStatement(typeof(Customer));

            Assert.Contains("INFORMATION_SCHEMA.TABLES", statement.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("[Customer]", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Table_exists_carries_the_name_and_schema_as_parameters()
        {
            var statement = Builder().GetTableExistsStatement(typeof(Customer));

            Assert.Equal(["Customer", "dbo"], statement.Parameters.Select(p => p.Value));
        }

        [Fact]
        public void Table_exists_uses_the_declared_schema_when_there_is_one()
        {
            var statement = Builder().GetTableExistsStatement(typeof(Invoice));

            Assert.Equal(["Invoice", "billing"], statement.Parameters.Select(p => p.Value));
        }

        [Fact]
        public void Create_table_declares_columns_constraints_and_the_foreign_key()
        {
            var statement = Builder().GetCreateTableStatement(typeof(Invoice));

            var expected =
                "CREATE TABLE [billing].[Invoice] \n" +
                "(\n" +
                "\t[Id] varchar(50) NOT NULL\n" +
                "\t,CONSTRAINT [PK_Invoice] PRIMARY KEY CLUSTERED([Id] ASC)\n" +
                "\t,[CustomerId] varchar(50)\n" +
                "\t,CONSTRAINT [FK_Invoice_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [Customer]([Id])\n" +
                "\t,[Amount] decimal(18,2) NOT NULL\n" +
                "\t,[Metadata] varchar(max)\n" +
                ")\n";

            Assert.Equal(expected, Normalise(statement.Text));
        }

        [Fact]
        public void An_ignored_property_is_not_a_column()
        {
            var statement = Builder().GetQueryStatement(typeof(Invoice));

            Assert.DoesNotContain("NotAColumn", statement.Text, StringComparison.Ordinal);
        }
    }
}

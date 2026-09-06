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
        public void Insert_names_every_column_except_the_key()
        {
            var statement = Builder().GetInsertStatement(typeof(Customer));

            Assert.Equal("INSERT INTO [Customer] ([Name]) VALUES (@Name)", statement.Text);
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
        public void Table_exists_probes_the_table_without_reading_it()
        {
            var statement = Builder().GetTableExistsStatement(typeof(Customer));

            Assert.Equal("SELECT TOP 1 * FROM [Customer]", statement.Text);
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

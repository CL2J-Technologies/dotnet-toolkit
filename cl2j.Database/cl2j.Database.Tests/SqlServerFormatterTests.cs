using cl2j.Database.CommandBuilders;
using cl2j.Database.Descriptors;
using cl2j.Database.Exceptions;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     The provider-specific half: how a name is quoted and how a CLR type becomes a column
    ///     declaration. These are characterisation tests — they lock in what the formatter does
    ///     today so a refactor cannot change it unnoticed. Where the current mapping looks wrong,
    ///     the test says so rather than pretending otherwise.
    /// </summary>
    public class SqlServerFormatterTests
    {
        private static IDatabaseFormatter Formatter()
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            return CommandBuilderFactory.GetCommandBuilder(new SqlConnection()).DatabaseFormatter;
        }

        private static ColumnDescriptor Column(Type type, string name)
            => TableDescriptorFactory.Create(type, Formatter()).Columns.Single(c => c.Name == name);

        [Fact]
        public void A_table_name_is_bracketed()
        {
            Assert.Equal("[Customer]", Formatter().FormatTableName("Customer"));
        }

        [Fact]
        public void A_schema_is_bracketed_separately()
        {
            Assert.Equal("[billing].[Invoice]", Formatter().FormatTableName("Invoice", "billing"));
        }

        [Fact]
        public void A_column_name_is_bracketed()
        {
            Assert.Equal("[Id]", Formatter().FormatColumnName("Id"));
        }

        [Fact]
        public void A_parameter_name_takes_an_at_sign()
        {
            Assert.Equal("@Id", Formatter().FormatParameterName("Id"));
        }

        [Theory]
        [InlineData("Flag", "bit")]
        [InlineData("Small", "smallint")]
        [InlineData("Count", "int")]
        [InlineData("Price", "decimal(18,2)")]
        [InlineData("Bounded", "varchar(64)")]
        [InlineData("Unbounded", "varchar(max)")]
        [InlineData("Moment", "datetimeoffset")]
        [InlineData("Stamp", "datetime2")]
        [InlineData("Kind", "int")]
        public void A_column_type_follows_the_property_type(string column, string expected)
        {
            Assert.Equal(expected, Formatter().GetColumnDataType(Column(typeof(AllTypesRow), column)));
        }

        [Fact]
        public void A_declared_type_name_wins_over_the_mapping()
        {
            Assert.Equal("geography", Formatter().GetColumnDataType(Column(typeof(AllTypesRow), "Declared")));
        }

        [Fact]
        public void A_default_is_appended_to_the_declaration()
        {
            Assert.Equal("varchar(10) DEFAULT 'n/a'", Formatter().GetColumnDataType(Column(typeof(AllTypesRow), "WithDefault")));
        }

        [Fact]
        public void A_json_column_is_varchar_max()
        {
            Assert.Equal("varchar(max)", Formatter().GetColumnDataType(Column(typeof(Invoice), "Metadata")));
        }

        [Fact]
        public void A_long_is_declared_int_which_cannot_hold_it()
        {
            //Characterisation, not endorsement. long and int share a branch, so a value past
            //int.MaxValue overflows the column the library creates for it. Left as-is here because
            //changing it alters the schema of every existing table; see the note on issue #15.
            Assert.Equal("int", Formatter().GetColumnDataType(Column(typeof(AllTypesRow), "Big")));
        }

        [Fact]
        public void A_double_is_declared_decimal_without_precision()
        {
            //Same: double shares the decimal branch, and with no Length it lands on bare decimal,
            //which SQL Server reads as decimal(18,0) — no fractional part at all.
            Assert.Equal("decimal", Formatter().GetColumnDataType(Column(typeof(AllTypesRow), "Ratio")));
        }

        [Fact]
        public void A_non_key_guid_falls_through_to_varchar()
        {
            //GetColumnKeyType knows about Guid; GetColumnDataType does not, so a Guid that is not a
            //key becomes text. Characterised so a fix is a visible change rather than a surprise.
            Assert.Equal("varchar(MAX)", Formatter().GetColumnDataType(Column(typeof(AllTypesRow), "Ref")));
        }

        [Fact]
        public void An_int_key_is_an_identity()
        {
            Assert.Equal(" IDENTITY(1,1) PRIMARY KEY", Formatter().GetColumnKeyType(Column(typeof(Counter), "Id")));
        }

        [Fact]
        public void A_string_key_is_only_required()
        {
            //The PRIMARY KEY constraint for a string key is added separately, by the CREATE TABLE
            //builder, as a named CONSTRAINT line.
            Assert.Equal(" NOT NULL", Formatter().GetColumnKeyType(Column(typeof(Customer), "Id")));
        }

        [Fact]
        public void A_key_type_the_provider_cannot_declare_is_refused()
        {
            var formatter = Formatter();
            var column = Column(typeof(UnsupportedKeyRow), "Id");

            Assert.Throws<DatabaseException>(() => formatter.GetColumnKeyType(column));
        }
    }
}

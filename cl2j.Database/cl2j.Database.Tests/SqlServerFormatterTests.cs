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
        public void A_long_is_declared_bigint()
        {
            //long used to share the int branch, so any value past int.MaxValue overflowed the
            //column the library had created for it. See issue #27.
            Assert.Equal("bigint", Formatter().GetColumnDataType(Column(typeof(AllTypesRow), "Big")));
        }

        [Fact]
        public void A_double_is_declared_float()
        {
            //double used to share the decimal branch and, with no Length, landed on bare decimal —
            //which SQL Server reads as decimal(18,0), so 1.5 was stored as 2.
            Assert.Equal("float", Formatter().GetColumnDataType(Column(typeof(AllTypesRow), "Ratio")));
        }

        [Fact]
        public void A_single_is_declared_real()
        {
            Assert.Equal("real", Formatter().GetColumnDataType(Column(typeof(AllTypesRow), "Rate")));
        }

        [Theory]
        [InlineData("MaybeCount", "int")]
        [InlineData("MaybeWhen", "datetime2")]
        [InlineData("MaybeMoment", "datetimeoffset")]
        [InlineData("MaybeRef", "uniqueidentifier")]
        [InlineData("MaybeFlag", "bit")]
        public void A_nullable_value_type_is_declared_like_the_type_it_wraps(string column, string expected)
        {
            //Nullable<int> is not int, so every one of these fell through to the varchar(MAX)
            //default — a perfectly ordinary nullable column, stored as text. See issue #27.
            Assert.Equal(expected, Formatter().GetColumnDataType(Column(typeof(NullablesRow), column)));
        }

        [Theory]
        [InlineData("Payload", "varbinary(max)")]
        [InlineData("Elapsed", "time")]
        [InlineData("Day", "date")]
        [InlineData("Tiny", "tinyint")]
        public void The_remaining_ordinary_types_have_a_mapping_of_their_own(string column, string expected)
        {
            Assert.Equal(expected, Formatter().GetColumnDataType(Column(typeof(NullablesRow), column)));
        }

        [Fact]
        public void A_decimal_without_a_declared_precision_is_refused()
        {
            //Bare decimal is decimal(18,0) to SQL Server, so an amount of 12.34 was stored as 12,
            //rounded on the way in with nothing to say so. Refusing is louder than guessing a
            //precision the caller did not choose. See issue #27.
            var formatter = Formatter();
            var column = Column(typeof(ImpreciseRow), "Amount");

            var exception = Assert.Throws<DatabaseException>(() => formatter.GetColumnDataType(column));

            Assert.Contains("Decimals", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void A_decimal_with_a_declared_precision_keeps_it()
        {
            Assert.Equal("decimal(18,2)", Formatter().GetColumnDataType(Column(typeof(AllTypesRow), "Price")));
        }

        [Fact]
        public void A_type_with_no_mapping_is_refused_rather_than_stored_as_text()
        {
            //The varchar(MAX) fallback is how a Guid ended up as text without anyone noticing. A
            //type nobody mapped is now a failure at schema creation, which is the cheapest moment
            //to find it. TypeName remains the escape hatch.
            var formatter = Formatter();
            var column = Column(typeof(UnmappableRow), "Thing");

            var exception = Assert.Throws<DatabaseException>(() => formatter.GetColumnDataType(column));

            Assert.Contains("Uri", exception.Message, StringComparison.Ordinal);
            Assert.Contains("TypeName", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void A_guid_is_declared_uniqueidentifier()
        {
            //GetColumnKeyType always knew about Guid; GetColumnDataType did not, so a Guid that was
            //not a key became text — 36 bytes instead of 16, with none of the type semantics.
            Assert.Equal("uniqueidentifier", Formatter().GetColumnDataType(Column(typeof(AllTypesRow), "Ref")));
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

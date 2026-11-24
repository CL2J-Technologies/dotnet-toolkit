using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Exceptions;
using cl2j.Database.Tests;
using cl2j.Database.TypeMatrix;
using Npgsql;
using Xunit;

namespace cl2j.Database.PostgreSql.Tests
{
    /// <summary>
    ///     The provider-specific half, with no server: how a name is quoted and how a CLR type
    ///     becomes a column declaration. Characterisation tests: they pin what the formatter does so
    ///     a refactor cannot change it unnoticed.
    /// </summary>
    public class PostgreSqlFormatterTests
    {
        [Table("Overridden")]
        private sealed class OverriddenRow
        {
            [Column(Name = "Name", Length = 50, TypeName = "citext")]
            public string Name { get; set; } = string.Empty;

            [Column(Name = "Body", Json = true)]
            public List<int>? Body { get; set; }

            [Column(Name = "BoundedBody", Json = true, Length = 200)]
            public List<int>? BoundedBody { get; set; }
        }

        private static IDatabaseFormatter Formatter()
        {
            cl2j.Database.PostgreSql.PostgreSql.Register();
            return CommandBuilderFactory.GetCommandBuilder(new NpgsqlConnection()).DatabaseFormatter;
        }

        private static ColumnDescriptor Column(Type type, string name)
            => TableDescriptorFactory.Create(type, Formatter()).Columns.Single(c => c.Name == name);

        [Fact]
        public void A_table_name_is_double_quoted()
        {
            Assert.Equal("\"Customer\"", Formatter().FormatTableName("Customer"));
        }

        [Fact]
        public void A_schema_is_quoted_separately()
        {
            Assert.Equal("\"billing\".\"Invoice\"", Formatter().FormatTableName("Invoice", "billing"));
        }

        [Fact]
        public void A_column_name_is_double_quoted()
        {
            Assert.Equal("\"Id\"", Formatter().FormatColumnName("Id"));
        }

        [Fact]
        public void A_double_quote_inside_a_name_is_doubled()
        {
            //Otherwise the name could end its own quoting and carry SQL after it.
            Assert.Equal("\"a\"\"b\"", Formatter().FormatColumnName("a\"b"));
            Assert.Equal("\"x\"\"; DROP TABLE t; --\"", Formatter().FormatTableName("x\"; DROP TABLE t; --"));
        }

        [Fact]
        public void A_parameter_name_takes_an_at_sign()
        {
            Assert.Equal("@Id", Formatter().FormatParameterName("Id"));
        }

        [Theory]
        [InlineData("Flag", "boolean")]
        [InlineData("Small", "smallint")]
        [InlineData("Count", "integer")]
        [InlineData("Big", "bigint")]
        [InlineData("Price", "numeric(18,2)")]
        [InlineData("Ratio", "double precision")]
        [InlineData("Rate", "real")]
        [InlineData("Bounded", "varchar(64)")]
        [InlineData("Unbounded", "text")]
        [InlineData("Moment", "timestamptz")]
        [InlineData("Stamp", "timestamp")]
        [InlineData("Ref", "uuid")]
        [InlineData("Kind", "integer")]
        public void A_column_type_follows_the_property_type(string column, string expected)
        {
            Assert.Equal(expected, Formatter().GetColumnDataType(Column(typeof(AllTypesRow), column)));
        }

        public static TheoryData<Type, string> Declarations => new()
        {
            { typeof(MxBool), "boolean" },
            { typeof(MxByte), "smallint" },
            { typeof(MxShort), "smallint" },
            { typeof(MxInt), "integer" },
            { typeof(MxLong), "bigint" },
            { typeof(MxFloat), "real" },
            { typeof(MxDouble), "double precision" },
            { typeof(MxDecimal), "numeric(18,2)" },
            { typeof(MxGuid), "uuid" },
            { typeof(MxString), "varchar(100)" },
            { typeof(MxBytes), "bytea" },
            { typeof(MxDateTime), "timestamp" },
            { typeof(MxDateTimeOffset), "timestamptz" },
            { typeof(MxDateOnly), "date" },
            { typeof(MxTimeOnly), "time" },
            { typeof(MxTimeSpan), "interval" },
            { typeof(MxChar), "char(1)" },
            { typeof(MxKind), "integer" },
        };

        [Theory]
        [MemberData(nameof(Declarations))]
        public void A_type_is_declared_the_same_plain_required_and_nullable(Type row, string expected)
        {
            var formatter = Formatter();

            Assert.Equal(expected, formatter.GetColumnDataType(Column(row, "V")));
            Assert.Equal(expected, formatter.GetColumnDataType(Column(row, "R")));
            Assert.Equal(expected, formatter.GetColumnDataType(Column(row, "N")));
        }

        [Theory]
        [InlineData("MaybeCount", "integer")]
        [InlineData("MaybeWhen", "timestamp")]
        [InlineData("MaybeMoment", "timestamptz")]
        [InlineData("MaybeRef", "uuid")]
        [InlineData("MaybeFlag", "boolean")]
        [InlineData("Payload", "bytea")]
        [InlineData("Elapsed", "interval")]
        [InlineData("Day", "date")]
        [InlineData("Tiny", "smallint")]
        public void A_nullable_value_type_is_declared_like_the_type_it_wraps(string column, string expected)
        {
            Assert.Equal(expected, Formatter().GetColumnDataType(Column(typeof(NullablesRow), column)));
        }

        [Fact]
        public void A_declared_type_name_wins_over_the_mapping()
        {
            Assert.Equal("geography", Formatter().GetColumnDataType(Column(typeof(AllTypesRow), "Declared")));
            Assert.Equal("citext", Formatter().GetColumnDataType(Column(typeof(OverriddenRow), "Name")));
        }

        [Fact]
        public void A_default_is_appended_to_the_declaration()
        {
            Assert.Equal("varchar(10) DEFAULT 'n/a'", Formatter().GetColumnDataType(Column(typeof(AllTypesRow), "WithDefault")));
        }

        [Fact]
        public void A_json_column_is_jsonb_and_a_length_makes_it_a_varchar()
        {
            Assert.Equal("jsonb", Formatter().GetColumnDataType(Column(typeof(OverriddenRow), "Body")));
            Assert.Equal("varchar(200)", Formatter().GetColumnDataType(Column(typeof(OverriddenRow), "BoundedBody")));
            Assert.Equal("jsonb", Formatter().GetColumnDataType(Column(typeof(Invoice), "Metadata")));
        }

        [Fact]
        public void A_decimal_without_a_declared_precision_is_refused()
        {
            var formatter = Formatter();
            var column = Column(typeof(ImpreciseRow), "Amount");

            var exception = Assert.Throws<DatabaseException>(() => formatter.GetColumnDataType(column));

            Assert.Contains("Decimals", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void A_type_with_no_mapping_is_refused_rather_than_stored_as_text()
        {
            var formatter = Formatter();
            var column = Column(typeof(UnmappableRow), "Thing");

            var exception = Assert.Throws<DatabaseException>(() => formatter.GetColumnDataType(column));

            Assert.Contains("Uri", exception.Message, StringComparison.Ordinal);
            Assert.Contains("TypeName", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void An_int_key_is_a_BY_DEFAULT_identity()
        {
            //BY DEFAULT rather than ALWAYS: a caller may want to seed a row with a key of its own.
            Assert.Equal(" GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY", Formatter().GetColumnKeyType(Column(typeof(Counter), "Id")));
        }

        [Fact]
        public void A_string_key_is_only_required()
        {
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

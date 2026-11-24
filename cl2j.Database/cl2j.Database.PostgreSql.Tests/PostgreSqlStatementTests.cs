using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Exceptions;
using cl2j.Database.Tests;
using Npgsql;
using Xunit;

namespace cl2j.Database.PostgreSql.Tests
{
    /// <summary>
    ///     The statements this provider produces are text, and wrong text is still valid text, so the
    ///     exact text is asserted. The shape is the shared base's; what is PostgreSQL's is the
    ///     quoting, the identity retrieval and the catalog query.
    /// </summary>
    public class PostgreSqlStatementTests
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

        [Table("DoubleKeyed")]
        private sealed class DoubleKeyedRow
        {
            [Column(Name = "Id", Key = KeyType.Key)]
            public double Id { get; set; }
        }

        [Table("JsonCarrier")]
        private sealed class JsonCarrierRow
        {
            [Column(Name = "Id", Key = KeyType.Key, Length = 10)]
            public string Id { get; set; } = string.Empty;

            [Column(Name = "Tags", Json = true)]
            public List<string>? Tags { get; set; }
        }

        [Table("OrderLine")]
        private sealed class OrderLineRow
        {
            [Column(Name = "OrderId", Key = KeyType.Key)]
            public int OrderId { get; set; }

            [Column(Name = "LineNo", Key = KeyType.Key)]
            public int LineNo { get; set; }

            [Column(Name = "Sku", Length = 20)]
            public string? Sku { get; set; }
        }

        private static ICommandBuilder Builder()
        {
            cl2j.Database.PostgreSql.PostgreSql.Register();
            return CommandBuilderFactory.GetCommandBuilder(new NpgsqlConnection());
        }

        //CREATE TABLE is built with AppendLine, so its line endings follow the machine.
        private static string Normalise(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

        [Fact]
        public void Insert_carries_a_string_key_like_any_other_column()
        {
            var statement = Builder().GetInsertStatement(typeof(Customer));

            Assert.Equal("INSERT INTO \"Customer\" (\"Id\",\"Name\") VALUES (@Id,@Name)", statement.Text);
        }

        [Fact]
        public void Insert_leaves_out_an_int_key_and_asks_for_it_back_with_RETURNING()
        {
            var statement = Builder().GetInsertStatement(typeof(Counter));

            Assert.Equal("INSERT INTO \"Counter\" (\"Label\") VALUES (@Label) RETURNING \"Id\"", statement.Text);
        }

        [Fact]
        public void Insert_asks_for_a_long_identity_back_the_same_way()
        {
            //The column is bigint, so RETURNING hands back a long without a cast.
            var statement = Builder().GetInsertStatement(typeof(BigKeyedRow));

            Assert.EndsWith(" RETURNING \"Id\"", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Insert_does_not_ask_for_an_identity_that_does_not_exist()
        {
            Assert.DoesNotContain("RETURNING", Builder().GetInsertStatement(typeof(Customer)).Text, StringComparison.Ordinal);
            Assert.DoesNotContain("RETURNING", Builder().GetInsertStatement(typeof(SelfKeyedRow)).Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Insert_leaves_a_guid_key_to_the_server()
        {
            var statement = Builder().GetInsertStatement(typeof(GuidKeyedRow));

            Assert.Equal("INSERT INTO \"GuidKeyed\" (\"Label\") VALUES (@Label)", statement.Text);
        }

        [Fact]
        public void A_json_value_is_bound_as_a_parameter_with_no_cast_in_the_text()
        {
            //The jsonb type travels with the parameter (see PrepareParameter), not as ::jsonb.
            var statement = Builder().GetInsertStatement(typeof(JsonCarrierRow));

            Assert.Equal("INSERT INTO \"JsonCarrier\" (\"Id\",\"Tags\") VALUES (@Id,@Tags)", statement.Text);
            Assert.DoesNotContain("::", Builder().GetUpdateStatement(typeof(JsonCarrierRow)).Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Update_sets_the_non_key_columns_and_filters_on_the_key()
        {
            Assert.Equal("UPDATE \"Customer\" SET \"Name\"=@Name WHERE \"Id\"=@Id", Builder().GetUpdateStatement(typeof(Customer)).Text);
            Assert.Equal("UPDATE \"Customer\" SET \"Name\"=@Name WHERE \"Id\"=@Id", Builder().GetUpdateColumnStatement(typeof(Customer), "Name").Text);
        }

        [Fact]
        public void Delete_filters_on_the_key()
        {
            Assert.Equal("DELETE FROM \"Customer\" WHERE \"Id\"=@Id", Builder().GetDeleteStatement(typeof(Customer)).Text);
        }

        [Fact]
        public void A_composite_key_joins_its_columns_with_AND()
        {
            Assert.Equal("DELETE FROM \"TenantScopedRow\" WHERE \"TenantId\"=@TenantId AND \"Id\"=@Id", Builder().GetDeleteStatement(typeof(TenantScopedRow)).Text);
        }

        [Fact]
        public void Query_selects_every_column_and_filters_on_a_parameter_type()
        {
            Assert.Equal("SELECT \"Id\",\"Name\" FROM \"Customer\"", Builder().GetQueryStatement(typeof(Customer)).Text);
            Assert.Equal("SELECT \"Id\",\"Name\" FROM \"Customer\" WHERE \"Name\"=@Name", Builder().GetQueryStatement(typeof(Customer), typeof(CustomerFilter)).Text);
        }

        [Fact]
        public void Drop_table_qualifies_the_schema_when_there_is_one()
        {
            Assert.Equal("DROP TABLE \"billing\".\"Invoice\"", Builder().GetDropTableStatement(typeof(Invoice)).Text);
            Assert.Equal("DROP TABLE \"Customer\"", Builder().GetDropTableStatement(typeof(Customer)).Text);
        }

        [Fact]
        public void Table_exists_asks_the_catalog_and_resolves_the_default_schema_on_the_server()
        {
            var statement = Builder().GetTableExistsStatement(typeof(Customer));

            Assert.Contains("INFORMATION_SCHEMA.TABLES", statement.Text, StringComparison.Ordinal);
            Assert.Contains("current_schema()", statement.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Customer\"", statement.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("dbo", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Table_exists_carries_the_name_and_the_declared_schema_as_parameters()
        {
            Assert.Equal(["Customer"], Builder().GetTableExistsStatement(typeof(Customer)).Parameters.Select(p => p.Value));
            Assert.Equal(["Invoice", "billing"], Builder().GetTableExistsStatement(typeof(Invoice)).Parameters.Select(p => p.Value));
        }

        [Fact]
        public void Create_table_declares_columns_constraints_and_the_foreign_key()
        {
            var statement = Builder().GetCreateTableStatement(typeof(Invoice));

            var expected =
                "CREATE TABLE \"billing\".\"Invoice\" \n" +
                "(\n" +
                "\t\"Id\" varchar(50) NOT NULL\n" +
                "\t,CONSTRAINT \"PK_Invoice\" PRIMARY KEY (\"Id\")\n" +
                "\t,\"CustomerId\" varchar(50)\n" +
                "\t,CONSTRAINT \"FK_Invoice_Customer\" FOREIGN KEY (\"CustomerId\") REFERENCES \"Customer\"(\"Id\")\n" +
                "\t,\"Amount\" numeric(18,2) NOT NULL\n" +
                "\t,\"Metadata\" jsonb\n" +
                ")\n";

            Assert.Equal(expected, Normalise(statement.Text));
        }

        [Fact]
        public void An_int_key_is_created_as_an_identity_and_a_guid_key_gets_a_server_side_default()
        {
            Assert.Contains("\"Id\" integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY", Normalise(Builder().GetCreateTableStatement(typeof(Counter)).Text), StringComparison.Ordinal);
            Assert.Contains("\"Id\" bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY", Normalise(Builder().GetCreateTableStatement(typeof(BigKeyedRow)).Text), StringComparison.Ordinal);
            Assert.Contains("\"Id\" uuid DEFAULT gen_random_uuid() PRIMARY KEY", Normalise(Builder().GetCreateTableStatement(typeof(GuidKeyedRow)).Text), StringComparison.Ordinal);
        }

        [Table("UnboundedKey")]
        private sealed class UnboundedKeyRow
        {
            [Column(Name = "Code", Key = KeyType.Key)]
            public string Code { get; set; } = string.Empty;
        }

        [Fact]
        public void A_string_key_without_a_length_is_refused_and_named()
        {
            //text would index, but a key with no declared size is almost always a forgotten Length,
            //and the other providers refuse it, so the schema must not depend on the provider.
            var ex = Assert.Throws<DatabaseException>(() => Builder().GetCreateTableStatement(typeof(UnboundedKeyRow)));

            Assert.Contains("Code", ex.Message, StringComparison.Ordinal);
            Assert.Contains("A string key needs a Length: [Column(Length = 50)]", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void A_composite_key_is_one_table_level_constraint_with_no_identity()
        {
            var text = Normalise(Builder().GetCreateTableStatement(typeof(OrderLineRow)).Text);

            var expected =
                "CREATE TABLE \"OrderLine\" \n" +
                "(\n" +
                "\t\"OrderId\" integer NOT NULL\n" +
                "\t,\"LineNo\" integer NOT NULL\n" +
                "\t,\"Sku\" varchar(20)\n" +
                "\t,CONSTRAINT \"PK_OrderLine\" PRIMARY KEY (\"OrderId\", \"LineNo\")\n" +
                ")\n";

            Assert.Equal(expected, text);
        }

        [Fact]
        public void Insert_sends_every_column_of_a_composite_key_and_asks_for_no_identity()
        {
            var statement = Builder().GetInsertStatement(typeof(OrderLineRow));

            Assert.Equal("INSERT INTO \"OrderLine\" (\"OrderId\",\"LineNo\",\"Sku\") VALUES (@OrderId,@LineNo,@Sku)", statement.Text);
        }

        [Fact]
        public void A_key_of_an_unsupported_type_is_refused_when_the_table_is_created()
        {
            var ex = Assert.Throws<DatabaseException>(() => Builder().GetCreateTableStatement(typeof(DoubleKeyedRow)));

            Assert.Equal("Unsupported key type 'Double'", ex.Message);
        }

        [Fact]
        public void QueryByKeys_refuses_a_composite_key()
        {
            var ex = Assert.Throws<DatabaseException>(() => Builder().GetQueryByKeysStatement(typeof(TenantScopedRow), ["a", "b"]));

            Assert.Equal("Only one Key must be defined for QueryByKeys", ex.Message);
        }

        [Fact]
        public void An_ignored_property_is_not_a_column()
        {
            Assert.DoesNotContain("NotAColumn", Builder().GetQueryStatement(typeof(Invoice)).Text, StringComparison.Ordinal);
        }
    }
}

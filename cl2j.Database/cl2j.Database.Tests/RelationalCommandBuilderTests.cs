using cl2j.Database.CommandBuilders;
using cl2j.Database.Descriptors;
using cl2j.Database.Exceptions;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     What <see cref="RelationalCommandBuilder"/> writes by itself, seen through a provider that
    ///     differs from SQL Server on every axis the base lets one differ on (see
    ///     <see cref="TestProviderCommandBuilder"/>): if a statement came out in SQL Server's words,
    ///     the base would be holding on to something it should have asked the provider for.
    /// </summary>
    public class RelationalCommandBuilderTests
    {
        private static TestProviderCommandBuilder Builder() => new();

        //CREATE TABLE and the table-exists text are built with the machine's line endings.
        private static string Normalise(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

        private static ColumnDescriptor Column(RelationalCommandBuilder builder, Type type, string name)
            => TableDescriptorFactory.Create(type, builder.DatabaseFormatter).Columns.Single(c => c.Name == name);

        #region Statements

        [Fact]
        public void Select_names_every_column_of_the_table_with_the_provider_quoting()
        {
            var statement = Builder().GetQueryStatement(typeof(TpStringKeyRow));

            Assert.Equal("SELECT `Id`,`Label` FROM `TpStringKey`", statement.Text);
        }

        [Fact]
        public void Select_by_example_filters_on_the_columns_of_the_example()
        {
            var statement = Builder().GetQueryStatement(typeof(TpPrepRow), typeof(TpPrepFilter));

            Assert.Equal("SELECT `Id`,`Name`,`Count` FROM `TpPrep` WHERE `Name`=@Name", statement.Text);
        }

        [Fact]
        public void Select_by_key_filters_on_the_key()
        {
            var statement = Builder().GetQueryByKeyStatement(typeof(TpStringKeyRow));

            Assert.Equal("SELECT `Id`,`Label` FROM `TpStringKey` WHERE `Id`=@Id", statement.Text);
        }

        [Fact]
        public void Select_by_keys_carries_one_generated_parameter_per_key()
        {
            var statement = Builder().GetQueryByKeysStatement(typeof(TpStringKeyRow), ["a", "b"]);

            Assert.Equal("SELECT `Id`,`Label` FROM `TpStringKey` WHERE `Id` IN (@key0,@key1) ", statement.Text);
            Assert.Equal(["key0", "key1"], statement.Parameters.Select(p => p.Name));
            Assert.Equal(["a", "b"], statement.Parameters.Select(p => p.Value));
        }

        [Fact]
        public void Select_by_keys_refuses_a_table_without_exactly_one_key()
        {
            Assert.Throws<DatabaseException>(() => Builder().GetQueryByKeysStatement(typeof(TenantScopedRow), ["a"]));
        }

        [Fact]
        public void Update_sets_the_non_key_columns_and_filters_on_the_key()
        {
            var statement = Builder().GetUpdateStatement(typeof(TpStringKeyRow));

            Assert.Equal("UPDATE `TpStringKey` SET `Label`=@Label WHERE `Id`=@Id", statement.Text);
        }

        [Fact]
        public void Update_of_one_column_sets_only_that_column()
        {
            var statement = Builder().GetUpdateColumnStatement(typeof(TpPrepRow), "Name");

            Assert.Equal("UPDATE `TpPrep` SET `Name`=@Name WHERE `Id`=@Id", statement.Text);
        }

        [Fact]
        public void Delete_filters_on_the_key()
        {
            var statement = Builder().GetDeleteStatement(typeof(TpStringKeyRow));

            Assert.Equal("DELETE FROM `TpStringKey` WHERE `Id`=@Id", statement.Text);
        }

        [Fact]
        public void Drop_qualifies_the_table_with_its_schema_when_it_has_one()
        {
            Assert.Equal("DROP TABLE `TpStringKey`", Builder().GetDropTableStatement(typeof(TpStringKeyRow)).Text);
            Assert.Equal("DROP TABLE `tpschema`.`TpSchemaRow`", Builder().GetDropTableStatement(typeof(TpSchemaRow)).Text);
        }

        [Fact]
        public void Create_table_uses_the_provider_types_and_key_declaration()
        {
            var statement = Builder().GetCreateTableStatement(typeof(TpIdentityRow));

            var expected =
                "CREATE TABLE `TpIdentity` \n" +
                "(\n" +
                "\t`Id` INT AUTO PK\n" +
                "\t,`Label` VARCHAR(50)\n" +
                ")\n";

            Assert.Equal(expected, Normalise(statement.Text));
        }

        [Fact]
        public void A_string_key_constraint_is_a_plain_primary_key_that_every_provider_accepts()
        {
            //MySQL refuses CLUSTERED and PostgreSQL refuses ASC, so the text carries neither.
            var statement = Builder().GetCreateTableStatement(typeof(TpStringKeyRow));

            var expected =
                "CREATE TABLE `TpStringKey` \n" +
                "(\n" +
                "\t`Id` VARCHAR(20) NOT NULL\n" +
                "\t,CONSTRAINT `PK_TpStringKey` PRIMARY KEY (`Id`)\n" +
                "\t,`Label` VARCHAR(50)\n" +
                ")\n";

            Assert.Equal(expected, Normalise(statement.Text));
            Assert.DoesNotContain("CLUSTERED", statement.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("ASC", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void The_builder_is_its_own_formatter()
        {
            var builder = Builder();

            Assert.Same(builder, builder.DatabaseFormatter);
        }

        [Fact]
        public void A_parameter_is_marked_with_an_at_sign_unless_the_provider_says_otherwise()
        {
            Assert.Equal("@Id", Builder().FormatParameterName("Id"));
        }

        [Fact]
        public void The_identifier_generator_defaults_to_the_guid_one()
        {
            Assert.Same(GuidIdentifierGenerator.Default, Builder().IdentifierGenerator);
        }

        #endregion

        #region Insert and identity

        [Fact]
        public void Insert_of_an_int_key_leaves_it_out_and_asks_for_the_identity()
        {
            var statement = Builder().GetInsertStatement(typeof(TpIdentityRow));

            Assert.Equal("INSERT INTO `TpIdentity` (`Label`) VALUES (@Label);SELECT ID()", statement.Text);
        }

        [Fact]
        public void Insert_of_a_long_key_asks_for_the_identity_too()
        {
            var statement = Builder().GetInsertStatement(typeof(TpIdentityLongRow));

            Assert.Equal("INSERT INTO `TpIdentityLong` (`Label`) VALUES (@Label);SELECT ID()", statement.Text);
        }

        [Fact]
        public void Insert_of_a_guid_key_leaves_it_out_without_asking_for_an_identity()
        {
            //The server fills a Guid in from a column default, and no "last identity" reports it.
            var statement = Builder().GetInsertStatement(typeof(TpGuidKeyRow));

            Assert.Equal("INSERT INTO `TpGuidKey` (`Label`) VALUES (@Label)", statement.Text);
        }

        [Fact]
        public void Insert_of_a_string_key_carries_it_and_does_not_ask_for_an_identity()
        {
            var statement = Builder().GetInsertStatement(typeof(TpStringKeyRow));

            Assert.Equal("INSERT INTO `TpStringKey` (`Id`,`Label`) VALUES (@Id,@Label)", statement.Text);
        }

        [Fact]
        public void Insert_of_a_self_generated_key_does_not_ask_for_an_identity()
        {
            var statement = Builder().GetInsertStatement(typeof(TpSelfGeneratedRow));

            Assert.DoesNotContain("ID()", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Insert_of_an_auto_generated_key_does_not_ask_for_an_identity()
        {
            var statement = Builder().GetInsertStatement(typeof(TpAutoGeneratedRow));

            Assert.DoesNotContain("ID()", statement.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void Insert_of_a_table_without_a_key_does_not_ask_for_an_identity()
        {
            var statement = Builder().GetInsertStatement(typeof(TpPrepFilter));

            Assert.DoesNotContain("ID()", statement.Text, StringComparison.Ordinal);
        }

        #endregion

        #region Table exists

        private const string ExistsHead = "SELECT CASE WHEN EXISTS (\n    SELECT 1 FROM INFORMATION_SCHEMA.TABLES\n    WHERE TABLE_NAME = @tableName AND TABLE_SCHEMA = ";
        private const string ExistsTail = "\n) THEN 1 ELSE 0 END";

        [Fact]
        public void Table_exists_asks_the_server_for_its_current_schema_when_nothing_names_one()
        {
            var statement = Builder().GetTableExistsStatement(typeof(TpIdentityRow));

            Assert.Equal(ExistsHead + "SCHEMA()" + ExistsTail, Normalise(statement.Text));
            Assert.Equal(["TpIdentity"], statement.Parameters.Select(p => p.Value));
        }

        [Fact]
        public void Table_exists_binds_the_schema_the_type_names()
        {
            var statement = Builder().GetTableExistsStatement(typeof(TpSchemaRow));

            Assert.Equal(ExistsHead + "@tableSchema" + ExistsTail, Normalise(statement.Text));
            Assert.Equal(["TpSchemaRow", "tpschema"], statement.Parameters.Select(p => p.Value));
        }

        [Fact]
        public void Table_exists_binds_the_default_schema_of_the_provider_when_the_type_names_none()
        {
            var statement = new TestProviderCommandBuilder(defaultSchemaName: "main").GetTableExistsStatement(typeof(TpIdentityRow));

            Assert.Equal(ExistsHead + "@tableSchema" + ExistsTail, Normalise(statement.Text));
            Assert.Equal(["TpIdentity", "main"], statement.Parameters.Select(p => p.Value));
        }

        [Fact]
        public void The_schema_a_type_names_wins_over_the_default_of_the_provider()
        {
            var statement = new TestProviderCommandBuilder(defaultSchemaName: "main").GetTableExistsStatement(typeof(TpSchemaRow));

            Assert.Equal(["TpSchemaRow", "tpschema"], statement.Parameters.Select(p => p.Value));
        }

        [Fact]
        public void A_provider_that_gives_neither_a_default_schema_nor_an_expression_is_refused_with_a_message_that_says_so()
        {
            var builder = new TestProviderCommandBuilder(providesCurrentSchema: false);

            var exception = Assert.Throws<NotSupportedException>(() => builder.GetTableExistsStatement(typeof(TpIdentityRow)));

            Assert.Contains("DefaultSchemaName", exception.Message, StringComparison.Ordinal);
            Assert.Contains("CurrentSchemaExpression", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void A_provider_with_a_default_schema_does_not_need_the_expression()
        {
            var builder = new TestProviderCommandBuilder(defaultSchemaName: "main", providesCurrentSchema: false);

            var statement = builder.GetTableExistsStatement(typeof(TpIdentityRow));

            Assert.Equal(["TpIdentity", "main"], statement.Parameters.Select(p => p.Value));
        }

        #endregion

        #region Column types

        [Fact]
        public void An_explicit_type_name_wins()
        {
            var builder = Builder();

            Assert.Equal("CUSTOMTYPE", builder.GetColumnDataType(Column(builder, typeof(TpColumnsRow), "Declared")));
        }

        [Fact]
        public void A_json_column_takes_the_provider_json_type()
        {
            var builder = Builder();

            Assert.Equal("JSON", builder.GetColumnDataType(Column(builder, typeof(TpColumnsRow), "Document")));
            Assert.Equal("VARCHAR(200)", builder.GetColumnDataType(Column(builder, typeof(TpColumnsRow), "BoundedDocument")));
        }

        [Fact]
        public void A_nullable_value_type_is_mapped_as_the_type_it_wraps()
        {
            //The test provider maps int and knows nothing of Nullable<int>: reaching it unwrapped is
            //the only way this comes out as INT.
            var builder = Builder();

            Assert.Equal("INT", builder.GetColumnDataType(Column(builder, typeof(TpColumnsRow), "Optional")));
        }

        [Fact]
        public void An_enum_reaches_the_provider_as_an_enum()
        {
            var builder = Builder();

            Assert.Equal("INT", builder.GetColumnDataType(Column(builder, typeof(TpColumnsRow), "Kind")));
        }

        [Fact]
        public void A_type_the_provider_does_not_map_is_refused_and_the_message_names_the_way_out()
        {
            var builder = Builder();

            var exception = Assert.Throws<DatabaseException>(() => builder.GetColumnDataType(Column(builder, typeof(TpColumnsRow), "Unmapped")));

            Assert.Contains("'Version'", exception.Message, StringComparison.Ordinal);
            Assert.Contains("'Unmapped'", exception.Message, StringComparison.Ordinal);
            Assert.Contains("TypeName", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void A_column_default_is_appended_to_the_type()
        {
            var builder = Builder();

            Assert.Equal("INT DEFAULT 7", builder.GetColumnDataType(Column(builder, typeof(TpColumnsRow), "WithDefault")));
            Assert.Equal("CUSTOMTYPE DEFAULT 1", builder.GetColumnDataType(Column(builder, typeof(TpColumnsRow), "DeclaredWithDefault")));
        }

        [Fact]
        public void A_decimal_carries_its_precision_and_scale()
        {
            var builder = Builder();

            Assert.Equal("decimal(18,2)", builder.GetColumnDataType(Column(builder, typeof(TpColumnsRow), "Price")));
        }

        [Fact]
        public void A_decimal_without_a_precision_is_refused()
        {
            var builder = Builder();

            var exception = Assert.Throws<DatabaseException>(() => builder.GetColumnDataType(Column(builder, typeof(TpColumnsRow), "NoPrecision")));

            Assert.Contains("precision", exception.Message, StringComparison.Ordinal);
        }

        #endregion

        #region Key declarations

        [Theory]
        [InlineData(typeof(TpIdentityRow), " AUTO PK")]
        [InlineData(typeof(TpIdentityLongRow), " AUTO PK")]
        [InlineData(typeof(TpStringKeyRow), " NOT NULL")]
        [InlineData(typeof(TpGuidKeyRow), " DEFAULT UUID() PK")]
        public void A_key_is_declared_by_its_type(Type entity, string expected)
        {
            var builder = Builder();

            Assert.Equal(expected, builder.GetColumnKeyType(Column(builder, entity, "Id")));
        }

        [Fact]
        public void A_key_of_another_type_is_refused()
        {
            var builder = Builder();

            var exception = Assert.Throws<DatabaseException>(() => builder.GetColumnKeyType(Column(builder, typeof(TpUnsupportedKeyRow), "Id")));

            Assert.Contains("Int16", exception.Message, StringComparison.Ordinal);
        }

        #endregion

        #region Bulk insert

        [Fact]
        public async Task A_bulk_insert_leaves_out_an_identity_key()
        {
            var builder = Builder();

            await builder.BulkInsert(new RecordingDbConnection(), [new TpBulkIdentityRow { Label = "a" }], CancellationToken.None);

            Assert.Equal(["Label"], builder.LastBulk!.Columns.Select(c => c.Name));
        }

        [Fact]
        public async Task A_bulk_insert_leaves_out_a_guid_key()
        {
            var builder = Builder();

            await builder.BulkInsert(new RecordingDbConnection(), [new TpBulkGuidRow { Label = "a" }], CancellationToken.None);

            Assert.Equal(["Label"], builder.LastBulk!.Columns.Select(c => c.Name));
        }

        [Fact]
        public async Task A_bulk_insert_keeps_a_string_key()
        {
            //Nothing generates a string key, so a bulk insert that left it out would send NULL into
            //a NOT NULL primary key.
            var builder = Builder();

            await builder.BulkInsert(new RecordingDbConnection(), [new TpBulkStringRow { Code = "x", Label = "a" }], CancellationToken.None);

            Assert.Equal(["Code", "Label"], builder.LastBulk!.Columns.Select(c => c.Name));
        }

        [Fact]
        public async Task A_bulk_insert_hands_the_rows_over_as_a_table_with_one_column_per_written_column()
        {
            var builder = Builder();
            TpBulkStringRow[] items = [new() { Code = "x", Label = "a" }, new() { Code = "y", Label = "b" }];

            await builder.BulkInsert(new RecordingDbConnection(), items, CancellationToken.None);

            var rows = builder.LastBulk!.Rows;
            Assert.Equal(["Code", "Label"], rows.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName));
            Assert.Equal(["x", "y"], rows.Rows.Cast<System.Data.DataRow>().Select(r => r["Code"]));
            Assert.Equal(["a", "b"], rows.Rows.Cast<System.Data.DataRow>().Select(r => r["Label"]));
            Assert.Equal("`TpBulkString`", rows.TableName);
        }

        [Fact]
        public async Task A_bulk_insert_passes_the_connection_the_table_the_token_and_the_transaction_through()
        {
            var builder = Builder();
            var connection = new RecordingDbConnection();
            using var source = new CancellationTokenSource();

            await builder.BulkInsert(connection, [new TpBulkStringRow { Code = "x" }], source.Token, null);

            var call = builder.LastBulk!;
            Assert.Same(connection, call.Connection);
            Assert.Equal("`TpBulkString`", call.Table.NameFormatted);
            Assert.Equal(source.Token, call.CancellationToken);
            Assert.Null(call.Transaction);
        }

        #endregion

        #region Batching

        [Fact]
        public void Keys_are_batched_by_what_the_provider_allows()
        {
            var builder = Builder();
            object[] keys = [.. Enumerable.Range(0, 7).Select(i => (object)$"k{i}")];

            var statements = QueryKeyBatches.Build(builder, typeof(TpStringKeyRow), keys).ToList();

            Assert.Equal([3, 3, 1], statements.Select(s => s.Parameters.Count));
        }

        [Fact]
        public void A_provider_that_says_nothing_keeps_the_library_default_batch_size()
        {
            //SQL Server does not override it.
            cl2j.Database.SqlServer.SqlServer.Register();
            var builder = CommandBuilderFactory.GetCommandBuilder(new Microsoft.Data.SqlClient.SqlConnection());

            Assert.Equal(2000, builder.MaxKeysPerStatement);
            Assert.Equal(QueryKeyBatches.MaxKeysPerStatement, builder.MaxKeysPerStatement);
        }

        [Fact]
        public void A_batch_size_that_is_not_positive_is_refused()
        {
            var builder = new TestProviderCommandBuilder(maxKeysPerStatement: 0);

            Assert.Throws<InvalidOperationException>(() => QueryKeyBatches.Build(builder, typeof(TpStringKeyRow), ["a"]).ToList());
        }

        #endregion
    }
}

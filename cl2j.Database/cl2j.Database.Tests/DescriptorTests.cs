using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Helpers;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     Everything downstream is built from a descriptor, and a descriptor is built by
    ///     reflection — which is where a rename stops mapping without anyone being told.
    /// </summary>
    public class DescriptorTests
    {
        private static IDatabaseFormatter Formatter()
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            return CommandBuilderFactory.GetCommandBuilder(new SqlConnection()).DatabaseFormatter;
        }

        private static TableDescriptor Describe(Type type) => TableDescriptorFactory.Create(type, Formatter());

        [Fact]
        public void Columns_keep_the_order_the_properties_are_declared_in()
        {
            //Ordering is not cosmetic: SELECT and INSERT list columns in this order, so a change
            //here silently changes every statement the package emits.
            var descriptor = Describe(typeof(TenantScopedRow));

            Assert.Equal(["TenantId", "Id", "Name"], descriptor.Columns.Select(c => c.Name));
        }

        [Fact]
        public void An_ignored_property_never_becomes_a_column()
        {
            var descriptor = Describe(typeof(Invoice));

            Assert.DoesNotContain(descriptor.Columns, c => c.Name == "NotAColumn");
        }

        [Fact]
        public void A_declared_column_name_replaces_the_property_name()
        {
            var descriptor = Describe(typeof(Customer));

            Assert.Contains(descriptor.Columns, c => c.Name == "Id" && c.NameFormatted == "[Id]");
        }

        [Fact]
        public void Length_and_type_name_reach_the_descriptor()
        {
            var declared = Describe(typeof(AllTypesRow)).Columns.Single(c => c.Name == "Declared");
            var bounded = Describe(typeof(AllTypesRow)).Columns.Single(c => c.Name == "Bounded");

            Assert.Equal("geography", declared.ColumnAtribute.TypeName);
            Assert.Equal(64, bounded.ColumnAtribute.Length);
        }

        [Fact]
        public void Declared_keys_are_collected_in_order()
        {
            var descriptor = Describe(typeof(TenantScopedRow));

            Assert.Equal(["TenantId", "Id"], descriptor.Keys.Select(c => c.Name));
        }

        [Fact]
        public void A_property_called_Id_becomes_the_key_when_none_is_declared()
        {
            //An undocumented convention, and worth knowing about: a type with no [Column(Key=...)]
            //anywhere still gets a key if it happens to have an Id property.
            var descriptor = Describe(typeof(ImplicitlyKeyed));

            var key = Assert.Single(descriptor.Keys);
            Assert.Equal("Id", key.Name);
            Assert.Equal(KeyType.Key, key.ColumnAtribute.Key);
        }

        [Fact]
        public void A_table_name_falls_back_to_the_type_name()
        {
            Assert.Equal("ImplicitlyKeyed", Describe(typeof(ImplicitlyKeyed)).Name);
        }

        [Fact]
        public void A_declared_table_name_and_schema_are_used()
        {
            var descriptor = Describe(typeof(Invoice));

            Assert.Equal("Invoice", descriptor.Name);
            Assert.Equal("[billing].[Invoice]", descriptor.NameFormatted);
        }

        [Fact]
        public void Table_metadata_reads_the_attribute()
        {
            var withAttribute = typeof(Invoice).GetTableMetaData();
            var without = typeof(ImplicitlyKeyed).GetTableMetaData();

            Assert.Equal("Invoice", withAttribute.Table);
            Assert.Equal("billing", withAttribute.Schema);
            Assert.Equal("ImplicitlyKeyed", without.Table);
            Assert.Null(without.Schema);
        }

        [Fact]
        public void Table_properties_exclude_the_ignored_ones()
        {
            var names = typeof(Invoice).GetTableProperties().Select(p => p.Name);

            Assert.DoesNotContain("NotAColumn", names);
        }

        [Fact]
        public void An_attribute_is_found_on_a_type_and_on_a_property()
        {
            Assert.NotNull(typeof(Invoice).GetAttribute<TableAttribute>());
            Assert.Null(typeof(ImplicitlyKeyed).GetAttribute<TableAttribute>());

            var property = typeof(Invoice).GetProperty(nameof(Invoice.CustomerId))!;
            Assert.NotNull(property.GetAttribute<ForeignKeyAttribute>());
            Assert.True(property.HasAttribute<ForeignKeyAttribute>());
        }

        [Fact]
        public void A_foreign_key_carries_its_reference()
        {
            var property = typeof(Invoice).GetProperty(nameof(Invoice.CustomerId))!;

            var foreignKey = property.GetAttribute<ForeignKeyAttribute>()!;

            Assert.Equal("FK_Invoice_Customer", foreignKey.Name);
            Assert.Equal("Customer", foreignKey.ReferenceTable);
            Assert.Equal("Id", foreignKey.ReferenceField);
            Assert.Null(foreignKey.ReferenceSchema);
        }

        [Fact]
        public void A_generated_key_is_a_guid_without_separators()
        {
            var column = Describe(typeof(Customer)).Columns.Single(c => c.Name == "Id");

            var key = GuidIdentifierGenerator.Default.GenerateKey(column);

            Assert.Equal(32, key.Length);
            Assert.DoesNotContain("-", key, StringComparison.Ordinal);
        }

        [Fact]
        public void A_generated_key_carries_its_prefix()
        {
            var column = Describe(typeof(PrefixedRow)).Columns.Single(c => c.Name == "Id");

            var key = GuidIdentifierGenerator.Default.GenerateKey(column);

            Assert.StartsWith("cus_", key, StringComparison.Ordinal);
            Assert.Equal(36, key.Length);
        }

        [Fact]
        public void Two_generated_keys_differ()
        {
            var column = Describe(typeof(Customer)).Columns.Single(c => c.Name == "Id");

            Assert.NotEqual(
                GuidIdentifierGenerator.Default.GenerateKey(column),
                GuidIdentifierGenerator.Default.GenerateKey(column));
        }
    }
}

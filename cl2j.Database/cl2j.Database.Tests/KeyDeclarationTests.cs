using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     How SQL Server declares server-generated keys. A <c>long</c> key was refused, although
    ///     INSERT already read its identity back; a <c>Guid</c> key repeated its type
    ///     (<c>uniqueidentifier UNIQUEIDENTIFIER DEFAULT NEWID()</c>).
    /// </summary>
    public class KeyDeclarationTests
    {
        private static ICommandBuilder Builder()
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            return CommandBuilderFactory.GetCommandBuilder(new SqlConnection());
        }

        private static ColumnDescriptor Column(Type type, string name)
            => TableDescriptorFactory.Create(type, Builder().DatabaseFormatter).Columns.Single(c => c.Name == name);

        [Fact]
        public void A_long_key_is_an_identity()
        {
            Assert.Equal(" IDENTITY(1,1) PRIMARY KEY", Builder().DatabaseFormatter.GetColumnKeyType(Column(typeof(LongKeyed), "Id")));
        }

        [Fact]
        public void A_guid_key_gets_a_default_without_repeating_its_type()
        {
            Assert.Equal(" DEFAULT NEWID() PRIMARY KEY", Builder().DatabaseFormatter.GetColumnKeyType(Column(typeof(GuidKeyed), "Id")));
        }

        [Fact]
        public void The_create_table_of_a_guid_key_names_the_type_once()
        {
            var text = Builder().GetCreateTableStatement(typeof(GuidKeyed)).Text;

            Assert.Contains("[Id] uniqueidentifier DEFAULT NEWID() PRIMARY KEY", text, StringComparison.Ordinal);
            Assert.DoesNotContain("UNIQUEIDENTIFIER", text, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(typeof(LongKeyed), true)]
        [InlineData(typeof(GuidKeyed), true)]
        [InlineData(typeof(StringKeyed), false)]
        public void Only_keys_the_server_generates_are_supplied_by_the_server(Type type, bool expected)
        {
            //BulkInsert and INSERT both rely on this to decide which columns to send.
            Assert.Equal(expected, CommandBuilderHelpers.IsSuppliedByTheServer(Column(type, "Id")));
        }

        [Fact]
        public void A_composite_key_is_one_table_level_constraint()
        {
            //A PRIMARY KEY on each column made the server refuse the table ("multiple primary
            //keys"). Every key column is plain NOT NULL and one constraint joins them.
            var text = Builder().GetCreateTableStatement(typeof(OrderLine)).Text.Replace("\r\n", "\n", StringComparison.Ordinal);

            var expected =
                "CREATE TABLE [OrderLine] \n" +
                "(\n" +
                "\t[OrderId] int NOT NULL\n" +
                "\t,[LineNo] int NOT NULL\n" +
                "\t,[Sku] varchar(20)\n" +
                "\t,CONSTRAINT [PK_OrderLine] PRIMARY KEY ([OrderId], [LineNo])\n" +
                ")\n";

            Assert.Equal(expected, text);
        }

        [Fact]
        public void A_composite_key_of_strings_is_one_table_level_constraint()
        {
            var text = Builder().GetCreateTableStatement(typeof(TenantScopedRow)).Text.Replace("\r\n", "\n", StringComparison.Ordinal);

            Assert.Contains("[TenantId] varchar(50) NOT NULL", text, StringComparison.Ordinal);
            Assert.Contains("CONSTRAINT [PK_TenantScopedRow] PRIMARY KEY ([TenantId], [Id])", text, StringComparison.Ordinal);
            Assert.Equal(1, text.Split("PRIMARY KEY", StringSplitOptions.None).Length - 1);
        }

        [Fact]
        public void A_composite_guid_key_has_no_default()
        {
            var text = Builder().GetCreateTableStatement(typeof(GuidPairKeyed)).Text;

            Assert.DoesNotContain("NEWID", text, StringComparison.Ordinal);
            Assert.Contains("PRIMARY KEY ([GroupId], [Code])", text, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(typeof(OrderLine), "OrderId")]
        [InlineData(typeof(OrderLine), "LineNo")]
        [InlineData(typeof(GuidPairKeyed), "GroupId")]
        public void No_column_of_a_composite_key_is_supplied_by_the_server(Type type, string name)
        {
            //INSERT and BulkInsert send every column this says the server does not supply.
            var column = Column(type, name);

            Assert.False(CommandBuilderHelpers.IsSuppliedByTheServer(column));
            Assert.False(CommandBuilderHelpers.IsIdentity(column));
        }

        [Fact]
        public void Insert_sends_every_column_of_a_composite_key_and_asks_for_no_identity()
        {
            var statement = Builder().GetInsertStatement(typeof(OrderLine));

            Assert.Equal("INSERT INTO [OrderLine] ([OrderId],[LineNo],[Sku]) VALUES (@OrderId,@LineNo,@Sku)", statement.Text);
        }

        [Fact]
        public void Update_and_delete_filter_on_every_column_of_a_composite_key()
        {
            var builder = Builder();

            Assert.Equal("UPDATE [OrderLine] SET [Sku]=@Sku WHERE [OrderId]=@OrderId AND [LineNo]=@LineNo", builder.GetUpdateStatement(typeof(OrderLine)).Text);
            Assert.Equal("DELETE FROM [OrderLine] WHERE [OrderId]=@OrderId AND [LineNo]=@LineNo", builder.GetDeleteStatement(typeof(OrderLine)).Text);
        }

        [Fact]
        public void A_single_int_key_is_still_an_identity()
        {
            Assert.True(CommandBuilderHelpers.IsIdentity(Column(typeof(LongKeyed), "Id")));
            Assert.True(CommandBuilderHelpers.IsSuppliedByTheServer(Column(typeof(LongKeyed), "Id")));
        }

        [Table("OrderLine")]
        public sealed class OrderLine
        {
            [Column(Name = "OrderId", Key = KeyType.Key)]
            public int OrderId { get; set; }

            [Column(Name = "LineNo", Key = KeyType.Key)]
            public int LineNo { get; set; }

            [Column(Name = "Sku", Length = 20)]
            public string? Sku { get; set; }
        }

        [Table("GuidPairKeyed")]
        public sealed class GuidPairKeyed
        {
            [Column(Name = "GroupId", Key = KeyType.Key)]
            public Guid GroupId { get; set; }

            [Column(Name = "Code", Key = KeyType.Key, Length = 20)]
            public string Code { get; set; } = string.Empty;
        }

        [Table("LongKeyed")]
        public sealed class LongKeyed
        {
            [Column(Name = "Id", Key = KeyType.Key)]
            public long Id { get; set; }
        }

        [Table("GuidKeyed")]
        public sealed class GuidKeyed
        {
            [Column(Name = "Id", Key = KeyType.Key)]
            public Guid Id { get; set; }
        }

        [Table("StringKeyed")]
        public sealed class StringKeyed
        {
            [Column(Name = "Id", Key = KeyType.Key, Length = 20)]
            public string Id { get; set; } = string.Empty;
        }
    }
}

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

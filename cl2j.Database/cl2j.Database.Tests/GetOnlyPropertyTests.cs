using System.Data;
using System.Data.Common;
using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Exceptions;
using cl2j.Database.Helpers;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     A property with no public setter that is not <c>[Ignore]</c>d is a mapped column. Insert
    ///     writes it, but the generated reader fills an entity with an object initializer, which a
    ///     get-only property refuses: the first read failed with a Roslyn CS0200 about code the
    ///     caller never wrote. The reader now says which property and what to do.
    ///
    ///     Every scenario uses its own entity type, because the compiled reader is cached by type.
    /// </summary>
    public class GetOnlyPropertyTests
    {
        private static TableDescriptor Describe(Type type)
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            var formatter = CommandBuilderFactory.GetCommandBuilder(new SqlConnection()).DatabaseFormatter;
            return TableDescriptorFactory.Create(type, formatter);
        }

        private static DataTable Table()
        {
            var table = new DataTable();
            table.Columns.Add("Id", typeof(string));
            table.Columns.Add("Total", typeof(int));
            table.Rows.Add("a", 3);
            return table;
        }

        [Fact]
        public async Task Read_names_the_type_and_the_property_that_has_no_setter()
        {
            var ex = await Assert.ThrowsAsync<DatabaseException>(
                () => Table().CreateDataReader().Read<GetOnlyRow>(Describe(typeof(GetOnlyRow))));

            Assert.Contains("GetOnlyRow", ex.Message, StringComparison.Ordinal);
            Assert.Contains("Total", ex.Message, StringComparison.Ordinal);
            Assert.Contains("[Ignore]", ex.Message, StringComparison.Ordinal);
            Assert.Contains("setter", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ReadSingle_names_the_type_and_the_property_that_has_no_setter()
        {
            var ex = await Assert.ThrowsAsync<DatabaseException>(
                () => Table().CreateDataReader().ReadSingle<GetOnlySingleRow>(Describe(typeof(GetOnlySingleRow))));

            Assert.Contains("GetOnlySingleRow.Total", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_private_setter_is_refused_like_a_missing_one()
        {
            var ex = await Assert.ThrowsAsync<DatabaseException>(
                () => Table().CreateDataReader().Read<PrivateSetterRow>(Describe(typeof(PrivateSetterRow))));

            Assert.Contains("PrivateSetterRow.Total", ex.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task An_ignored_get_only_property_and_an_init_only_property_still_read()
        {
            var rows = await Table().CreateDataReader().Read<IgnoredAndInitRow>(Describe(typeof(IgnoredAndInitRow)));

            var row = Assert.Single(rows);
            Assert.Equal("a", row.Id);
            Assert.Equal(3, row.Total);
            Assert.Equal("a-3", row.Summary);
        }

        [Fact]
        public async Task Insert_still_writes_a_get_only_property()
        {
            CommandBuilderFactory.Register(new TestProviderCommandBuilder());
            var connection = new RecordingDbConnection();

            await connection.Insert(new WrittenGetOnlyRow(), CancellationToken.None);

            var parameters = connection.Commands.SelectMany(c => c.Parameters.Cast<DbParameter>()).ToList();
            Assert.Contains(parameters, p => p.Value is int total && total == 7);
        }

        [Table("GetOnly")]
        public sealed class GetOnlyRow
        {
            [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
            public string Id { get; set; } = string.Empty;

            [Column(Name = "Total", Required = true)]
            public int Total { get; } = 7;
        }

        [Table("GetOnlySingle")]
        public sealed class GetOnlySingleRow
        {
            [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
            public string Id { get; set; } = string.Empty;

            [Column(Name = "Total", Required = true)]
            public int Total { get; } = 7;
        }

        [Table("PrivateSetter")]
        public sealed class PrivateSetterRow
        {
            [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
            public string Id { get; set; } = string.Empty;

            [Column(Name = "Total", Required = true)]
            public int Total { get; private set; }
        }

        [Table("IgnoredAndInit")]
        public sealed class IgnoredAndInitRow
        {
            [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
            public string Id { get; init; } = string.Empty;

            [Column(Name = "Total", Required = true)]
            public int Total { get; set; }

            [Ignore]
            public string Summary => $"{Id}-{Total}";
        }

        [Table("WrittenGetOnly")]
        public sealed class WrittenGetOnlyRow
        {
            [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
            public string Id { get; set; } = "k";

            [Column(Name = "Total", Required = true)]
            public int Total { get; } = 7;
        }
    }
}

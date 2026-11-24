using System.Data;
using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Helpers;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     The in-memory table a bulk insert is built from: which columns it declares and what ends
    ///     up in each cell.
    /// </summary>
    public class DataTableHelpersTests
    {
        public sealed class Payload
        {
            public string Text { get; set; } = string.Empty;
        }

        [Table("BulkRow")]
        public sealed class BulkRow
        {
            [Column(Name = "Name", Length = 20)]
            public string Name { get; set; } = string.Empty;

            [Column(Name = "Quantity")]
            public int? Quantity { get; set; }

            [Column(Name = "Body", Json = true)]
            public Payload? Body { get; set; }

            [Column(Name = "CreatedAt")]
            public DateTimeOffset CreatedAt { get; set; }

            [Column(Name = "SeenAt", Default = "SYSDATETIMEOFFSET()")]
            public DateTimeOffset SeenAt { get; set; }
        }

        private static List<ColumnDescriptor> Columns()
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            var formatter = CommandBuilderFactory.GetCommandBuilder(new SqlConnection()).DatabaseFormatter;
            return TableDescriptorFactory.Create(typeof(BulkRow), formatter).Columns.ToList();
        }

        private static DataTable Build(params BulkRow[] items) => DataTableHelpers.CreateDataTable(items, "BulkRow", Columns());

        [Fact]
        public void The_table_carries_the_name_it_was_given()
        {
            Assert.Equal("BulkRow", Build().TableName);
        }

        [Fact]
        public void A_column_is_declared_for_each_descriptor_with_a_nullable_type_unwrapped()
        {
            var table = Build();

            Assert.Equal(["Name", "Quantity", "Body", "CreatedAt", "SeenAt"], table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
            Assert.Equal(typeof(int), table.Columns["Quantity"]!.DataType);
            Assert.Equal(typeof(DateTimeOffset), table.Columns["CreatedAt"]!.DataType);
        }

        [Fact]
        public void A_json_column_is_declared_as_text()
        {
            Assert.Equal(typeof(string), Build().Columns["Body"]!.DataType);
        }

        [Fact]
        public void No_item_gives_no_row()
        {
            Assert.Empty(Build().Rows);
        }

        [Fact]
        public void A_null_value_becomes_DBNull()
        {
            var row = Build(new BulkRow { Name = "a", Quantity = null, Body = null }).Rows[0];

            Assert.Equal(DBNull.Value, row["Quantity"]);
            Assert.Equal(DBNull.Value, row["Body"]);
        }

        [Fact]
        public void A_value_is_copied_as_it_is()
        {
            var row = Build(new BulkRow { Name = "widget", Quantity = 7 }).Rows[0];

            Assert.Equal("widget", row["Name"]);
            Assert.Equal(7, row["Quantity"]);
        }

        [Fact]
        public void A_json_value_is_serialised_to_text()
        {
            var row = Build(new BulkRow { Body = new Payload { Text = "hi" } }).Rows[0];

            Assert.Equal(ConnectionExtensions.ToJsonString(new Payload { Text = "hi" }), row["Body"]);
            Assert.Contains("hi", (string)row["Body"], StringComparison.Ordinal);
        }

        [Fact]
        public void A_DateTimeOffset_without_a_default_keeps_the_value_it_has()
        {
            //Only a null is replaced by the current time, as Insert does and the README says. The
            //table used to stamp every value, which replaced what the caller had set.
            var old = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));

            var row = Build(new BulkRow { CreatedAt = old }).Rows[0];

            Assert.Equal(old, (DateTimeOffset)row["CreatedAt"]);
            Assert.Equal(TimeSpan.FromHours(2), ((DateTimeOffset)row["CreatedAt"]).Offset);
        }

        [Fact]
        public void A_DateTimeOffset_with_a_default_keeps_the_value_it_has()
        {
            var seen = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

            var row = Build(new BulkRow { SeenAt = seen }).Rows[0];

            Assert.Equal(seen, (DateTimeOffset)row["SeenAt"]);
        }

        [Fact]
        public void Every_item_becomes_a_row_in_order()
        {
            var table = Build(new BulkRow { Name = "a" }, new BulkRow { Name = "b" }, new BulkRow { Name = "c" });

            Assert.Equal(["a", "b", "c"], table.Rows.Cast<DataRow>().Select(r => (string)r["Name"]));
        }
    }
}

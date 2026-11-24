using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using MySqlConnector;
using Xunit;

namespace cl2j.Database.MySql.IntegrationTests
{
    /// <summary>
    ///     The text of a composite-key table, which needs no server. MySQL has no unit-test project,
    ///     so this lives beside the integration tests; it is not in the server collection and starts
    ///     no container.
    /// </summary>
    public class MySqlCompositeKeyStatementTests
    {
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
            cl2j.Database.MySql.MySql.Register();
            return CommandBuilderFactory.GetCommandBuilder(new MySqlConnection());
        }

        private static string Normalise(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

        [Fact]
        public void A_composite_key_is_one_table_level_constraint_with_no_auto_increment()
        {
            var text = Normalise(Builder().GetCreateTableStatement(typeof(OrderLineRow)).Text);

            Assert.Contains("`OrderId` int NOT NULL", text, StringComparison.Ordinal);
            Assert.Contains("`LineNo` int NOT NULL", text, StringComparison.Ordinal);
            Assert.Contains("CONSTRAINT `PK_OrderLine` PRIMARY KEY (`OrderId`, `LineNo`)", text, StringComparison.Ordinal);
            Assert.DoesNotContain("AUTO_INCREMENT", text, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, text.Split("PRIMARY KEY", StringSplitOptions.None).Length - 1);
        }

        [Fact]
        public void Insert_sends_every_column_of_a_composite_key()
        {
            var text = Builder().GetInsertStatement(typeof(OrderLineRow)).Text;

            Assert.Contains("`OrderId`,`LineNo`,`Sku`", text, StringComparison.Ordinal);
            Assert.DoesNotContain("LAST_INSERT_ID", text, StringComparison.OrdinalIgnoreCase);
        }
    }
}

using System.Data;
using cl2j.Database.CommandBuilders;
using cl2j.Database.Descriptors;
using cl2j.Database.Helpers;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     Left to itself, SqlClient declares a parameter from its value: <c>nvarchar(3)</c> for
    ///     "abc", <c>nvarchar(10)</c> for a ten-character string, <c>decimal(3,1)</c> for 45.5,
    ///     <c>decimal(14,12)</c> for 45.123456789012, and <c>nvarchar(4000)</c> for any null. SQL
    ///     Server caches plans by the text of the batch, declarations included, so every new
    ///     length, scale or null is a new text: one more compilation, one more plan in the cache.
    ///
    ///     <para>
    ///     These tests hold the declaration to the type rather than to the value. They also hold
    ///     the one thing a fixed declaration must never do: cut a value that does not fit it.
    ///     </para>
    /// </summary>
    public class ParameterDeclarationTests
    {
        private static TableDescriptor Describe(Type type)
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            var formatter = CommandBuilderFactory.GetCommandBuilder(new SqlConnection()).DatabaseFormatter;
            return TableDescriptorFactory.Create(type, formatter);
        }

        //The path every object parameter takes: one column per property, its value read from the object.
        private static SqlParameter Bind(object param, string name)
        {
            var column = Describe(param.GetType()).Columns.Single(c => c.Name == name);
            return (SqlParameter)ParameterDeclaration.Bind(new SqlCommand(), name, column, column.Property.GetValue(param));
        }

        [Fact]
        public void Strings_of_different_lengths_are_declared_alike()
        {
            var shortOne = Bind(new { Name = "abc" }, "Name");
            var longOne = Bind(new { Name = "abcdefghij" }, "Name");

            Assert.Equal(SqlDbType.NVarChar, shortOne.SqlDbType);
            Assert.Equal(4000, shortOne.Size);
            Assert.Equal(shortOne.Size, longOne.Size);
        }

        [Fact]
        public void A_string_longer_than_the_common_size_is_declared_max_and_kept_whole()
        {
            //A size smaller than the value is not an error to SqlClient: it sends the first Size
            //characters and says nothing. Past the common size, the declaration has to widen.
            var text = new string('é', 4001);

            var parameter = Bind(new { Name = text }, "Name");

            Assert.Equal(-1, parameter.Size);
            Assert.Equal(text, parameter.Value);
        }

        [Fact]
        public void A_string_at_the_common_size_still_fits_it()
        {
            var parameter = Bind(new { Name = new string('a', 4000) }, "Name");

            Assert.Equal(4000, parameter.Size);
        }

        [Fact]
        public void A_string_key_keeps_its_ansi_binding_at_the_ansi_size()
        {
            var parameter = Bind(new Customer { Id = "cust-1" }, "Id");

            Assert.Equal(DbType.AnsiString, parameter.DbType);
            Assert.Equal(8000, parameter.Size);
        }

        [Fact]
        public void Decimals_of_different_scales_are_declared_alike()
        {
            var coarse = Bind(new { Lat = 45.5m }, "Lat");
            var fine = Bind(new { Lat = 45.123456789012m }, "Lat");

            Assert.Equal((byte)38, coarse.Precision);
            Assert.Equal((byte)18, coarse.Scale);
            Assert.Equal((coarse.Precision, coarse.Scale), (fine.Precision, fine.Scale));
        }

        [Theory]
        [InlineData("0.1234567890123456789012345")]
        [InlineData("123456789012345678901234")]
        public void A_decimal_the_fixed_declaration_cannot_hold_is_left_to_the_provider(string text)
        {
            //Declaring it decimal(38,18) anyway would round the first and overflow the second.
            var value = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

            var parameter = Bind(new { Amount = value }, "Amount");

            Assert.Equal((byte)0, parameter.Precision);
            Assert.Equal((byte)0, parameter.Scale);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void A_null_is_declared_with_the_type_of_its_property()
        {
            Assert.Equal(DbType.DateTimeOffset, Bind(new { When = (DateTimeOffset?)null }, "When").DbType);
            Assert.Equal(DbType.Int32, Bind(new { Count = (int?)null }, "Count").DbType);
            Assert.Equal(DbType.Guid, Bind(new { Ref = (Guid?)null }, "Ref").DbType);
        }

        [Fact]
        public void A_null_is_declared_like_a_value_of_the_same_property()
        {
            //The point of the null rule: a query run once with a value and once without should be
            //one text, not two.
            var none = Bind(new { When = (DateTimeOffset?)null }, "When");
            var some = Bind(new { When = (DateTimeOffset?)DateTimeOffset.UtcNow }, "When");

            Assert.Equal(some.SqlDbType, none.SqlDbType);
            Assert.Equal(DBNull.Value, none.Value);
        }

        [Fact]
        public void A_null_string_and_a_null_decimal_take_the_fixed_declarations()
        {
            var text = Bind(new { Name = (string?)null }, "Name");
            var number = Bind(new { Lat = (decimal?)null }, "Lat");

            Assert.Equal((DbType.String, 4000), (text.DbType, text.Size));
            Assert.Equal((DbType.Decimal, (byte)38, (byte)18), (number.DbType, number.Precision, number.Scale));
        }

        [Fact]
        public void A_null_string_key_binds_like_a_non_null_one()
        {
            var parameter = Bind(new Customer { Id = null! }, "Id");

            Assert.Equal((DbType.AnsiString, 8000), (parameter.DbType, parameter.Size));
        }
    }
}

using System.Data;
using cl2j.Database.CommandBuilders;
using cl2j.Database.Descriptors;
using cl2j.Database.Helpers;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     The nulls <see cref="ParameterDeclaration"/> cannot take a type for from a value: with no
    ///     column to read the property from, with a byte array, and with a reference type the
    ///     provider can only infer from a value.
    /// </summary>
    public class ParameterDeclarationNullTests
    {
        private static ColumnDescriptor Column(object shape, string name)
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            var formatter = CommandBuilderFactory.GetCommandBuilder(new SqlConnection()).DatabaseFormatter;
            return TableDescriptorFactory.Create(shape.GetType(), formatter).Columns.Single(c => c.Name == name);
        }

        [Fact]
        public void A_null_with_no_column_is_left_to_the_provider()
        {
            var parameter = (SqlParameter)ParameterDeclaration.Bind(new SqlCommand(), "p", null, null);

            Assert.Equal(DBNull.Value, parameter.Value);
            Assert.Equal(DbType.String, parameter.DbType);
        }

        [Fact]
        public void A_null_byte_array_is_declared_binary()
        {
            var shape = new { Payload = (byte[]?)null };

            var parameter = (SqlParameter)ParameterDeclaration.Bind(new SqlCommand(), "Payload", Column(shape, "Payload"), shape.Payload);

            Assert.Equal(DbType.Binary, parameter.DbType);
            Assert.Equal(DBNull.Value, parameter.Value);
        }

        [Fact]
        public void A_null_of_a_reference_type_the_provider_cannot_infer_keeps_the_providers_default()
        {
            var shape = new { Tags = (List<string>?)null };

            var parameter = (SqlParameter)ParameterDeclaration.Bind(new SqlCommand(), "Tags", Column(shape, "Tags"), shape.Tags);

            Assert.Equal(DBNull.Value, parameter.Value);
            Assert.Equal(DbType.String, parameter.DbType);
        }
    }
}

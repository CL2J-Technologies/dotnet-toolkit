using System.Data;
using System.Data.Common;
using cl2j.Database.CommandBuilders;
using cl2j.Database.DataAnnotations;
using cl2j.Database.Descriptors;
using cl2j.Database.Helpers;
using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     Npgsql refuses a CLR enum parameter unless it is given a PostgreSQL enum type name, and
    ///     every enum column this library creates is an integer. So an enum travels as its Int32
    ///     value, declared Int32, and a null enum is declared Int32 too. The recording parameter
    ///     defaults to <c>String</c> and infers nothing, so these tests see exactly what the core
    ///     decided and not what a provider would have inferred.
    /// </summary>
    public class EnumParameterTests
    {
        private static ColumnDescriptor Column(Type type, string name)
        {
            cl2j.Database.SqlServer.SqlServer.Register();
            var formatter = CommandBuilderFactory.GetCommandBuilder(new SqlConnection()).DatabaseFormatter;
            return TableDescriptorFactory.Create(type, formatter).Columns.Single(c => c.Name == name);
        }

        private static DbParameter Bind(Type type, string column, object? value)
        {
            var command = new RecordingDbConnection().CreateCommand();
            return ParameterDeclaration.Bind(command, column, Column(type, column), value);
        }

        private static async Task<DbParameter> SingleParameter(Func<RecordingDbConnection, Task> run)
        {
            CommandBuilderFactory.Register(new TestProviderCommandBuilder());
            var connection = new RecordingDbConnection();
            await run(connection);
            return connection.Commands.SelectMany(c => c.Parameters.Cast<DbParameter>()).Single();
        }

        [Fact]
        public void An_enum_value_is_bound_as_its_Int32_value()
        {
            var parameter = Bind(typeof(EnumParamRow), "Kind", SampleKind.Second);

            Assert.Equal(DbType.Int32, parameter.DbType);
            Assert.Equal(1, Assert.IsType<int>(parameter.Value));
        }

        [Fact]
        public void A_nullable_enum_value_is_bound_as_its_Int32_value()
        {
            var parameter = Bind(typeof(EnumParamRow), "MaybeKind", SampleKind.Second);

            Assert.Equal(DbType.Int32, parameter.DbType);
            Assert.Equal(1, Assert.IsType<int>(parameter.Value));
        }

        [Fact]
        public void An_enum_with_a_byte_underlying_type_is_bound_as_Int32_too()
        {
            var parameter = Bind(typeof(EnumParamRow), "Small", SmallKind.Big);

            Assert.Equal(DbType.Int32, parameter.DbType);
            Assert.Equal(200, Assert.IsType<int>(parameter.Value));
        }

        [Fact]
        public void A_null_enum_is_declared_Int32()
        {
            var parameter = Bind(typeof(EnumParamRow), "MaybeKind", null);

            Assert.Equal(DbType.Int32, parameter.DbType);
            Assert.Equal(DBNull.Value, parameter.Value);
        }

        [Fact]
        public void A_null_of_a_nullable_enum_property_is_declared_Int32_with_a_database_null()
        {
            var shape = new { Kind = (SampleKind?)null };

            var parameter = ParameterDeclaration.Bind(new RecordingDbConnection().CreateCommand(), "Kind", Column(shape.GetType(), "Kind"), shape.Kind);

            Assert.Equal(DbType.Int32, parameter.DbType);
            Assert.Equal(DBNull.Value, parameter.Value);
        }

        [Fact]
        public void An_enum_still_binds_as_Int32_on_SqlClient()
        {
            var parameter = (SqlParameter)ParameterDeclaration.Bind(new SqlCommand(), "Kind", Column(typeof(EnumParamRow), "Kind"), SampleKind.Second);

            Assert.Equal(SqlDbType.Int, parameter.SqlDbType);
            Assert.Equal(1, parameter.Value);

            var none = (SqlParameter)ParameterDeclaration.Bind(new SqlCommand(), "MaybeKind", Column(typeof(EnumParamRow), "MaybeKind"), null);

            Assert.Equal(SqlDbType.Int, none.SqlDbType);
        }

        [Fact]
        public async Task An_enum_in_an_object_parameter_reaches_the_rule()
        {
            var parameter = await SingleParameter(c => c.Execute("DELETE", new EnumParamFilter { Kind = SampleKind.Second }, CancellationToken.None));

            Assert.Equal(DbType.Int32, parameter.DbType);
            Assert.Equal(1, Assert.IsType<int>(parameter.Value));
        }

        [Fact]
        public async Task An_enum_key_reaches_the_rule()
        {
            var parameter = await SingleParameter(c => c.DeleteKey<EnumKeyRow>(SampleKind.Second, CancellationToken.None));

            Assert.Equal(DbType.Int32, parameter.DbType);
            Assert.Equal(1, Assert.IsType<int>(parameter.Value));
        }

        [Fact]
        public async Task An_enum_in_UpdateColumn_reaches_the_rule()
        {
            CommandBuilderFactory.Register(new TestProviderCommandBuilder());
            var connection = new RecordingDbConnection();

            await connection.UpdateColumn(new EnumKeyRow { Kind = SampleKind.First, Other = SampleKind.First }, "Other", SampleKind.Second);

            var parameters = connection.Commands.SelectMany(c => c.Parameters.Cast<DbParameter>()).ToList();
            Assert.Equal(["Other", "Kind"], parameters.Select(p => p.ParameterName));
            Assert.All(parameters, p => Assert.Equal(DbType.Int32, p.DbType));
            Assert.Equal([1, 0], parameters.Select(p => Assert.IsType<int>(p.Value)));
        }
    }

    public enum SmallKind : byte
    {
        Small = 1,
        Big = 200
    }

    [Table("EnumParam")]
    public sealed class EnumParamRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Kind")]
        public SampleKind Kind { get; set; }

        [Column(Name = "MaybeKind")]
        public SampleKind? MaybeKind { get; set; }

        [Column(Name = "Small")]
        public SmallKind Small { get; set; }
    }

    [Table("EnumParam")]
    public sealed class EnumParamFilter
    {
        [Column(Name = "Kind")]
        public SampleKind Kind { get; set; }
    }

    [Table("EnumKey")]
    public sealed class EnumKeyRow
    {
        [Column(Name = "Kind", Key = KeyType.Key)]
        public SampleKind Kind { get; set; }

        [Column(Name = "Other")]
        public SampleKind Other { get; set; }
    }
}

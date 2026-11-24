using cl2j.Database.CommandBuilders;
using cl2j.Database.Helpers;
using Xunit;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     <c>Insert&lt;TIn, TOut&gt;</c> used to unbox the scalar the insert returned, which only works
    ///     when the server reports the identity in exactly the type the caller declared. MySQL reports
    ///     a <c>ulong</c>.
    /// </summary>
    public class IdentityConversionTests
    {
        [Fact]
        public void A_value_of_the_requested_type_is_returned_as_it_is()
        {
            Assert.Equal(7, IdentityConversion.To<int>(7));
        }

        [Fact]
        public void An_unsigned_long_becomes_an_int()
        {
            //What MySQL's LAST_INSERT_ID() returns.
            Assert.Equal(7, IdentityConversion.To<int>(7UL));
        }

        [Fact]
        public void An_unsigned_long_becomes_a_long()
        {
            Assert.Equal(9_000_000_000L, IdentityConversion.To<long>(9_000_000_000UL));
        }

        [Fact]
        public void A_decimal_becomes_an_int()
        {
            //What SCOPE_IDENTITY() returns when it is not cast.
            Assert.Equal(7, IdentityConversion.To<int>(7m));
        }

        [Fact]
        public void A_long_becomes_a_nullable_int()
        {
            Assert.Equal(7, IdentityConversion.To<int?>(7L));
        }

        [Fact]
        public void A_nullable_target_accepts_a_value_of_its_own_type()
        {
            Assert.Equal(7, IdentityConversion.To<int?>(7));
        }

        [Fact]
        public void Null_is_the_default()
        {
            Assert.Equal(0, IdentityConversion.To<int>(null));
            Assert.Null(IdentityConversion.To<int?>(null));
            Assert.Null(IdentityConversion.To<string>(null));
        }

        [Fact]
        public void A_database_null_is_the_default()
        {
            Assert.Equal(0, IdentityConversion.To<int>(DBNull.Value));
            Assert.Null(IdentityConversion.To<long?>(DBNull.Value));
        }

        [Fact]
        public void A_value_too_big_for_the_target_is_an_error_rather_than_a_wrapped_number()
        {
            Assert.Throws<OverflowException>(() => IdentityConversion.To<int>(9_000_000_000UL));
            Assert.Throws<OverflowException>(() => IdentityConversion.To<int?>(9_000_000_000L));
        }

        [Fact]
        public void A_guid_and_a_string_pass_through()
        {
            var guid = Guid.NewGuid();

            Assert.Equal(guid, IdentityConversion.To<Guid>(guid));
            Assert.Equal("abc", IdentityConversion.To<string>("abc"));
        }

        [Fact]
        public void A_request_for_object_gets_the_value_unchanged()
        {
            Assert.Equal(7UL, IdentityConversion.To<object>(7UL));
        }

        [Fact]
        public void The_conversion_does_not_depend_on_the_culture_of_the_machine()
        {
            var current = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");

                Assert.Equal(12, IdentityConversion.To<int>("12"));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = current;
            }
        }

        [Fact]
        public async Task Insert_with_a_typed_result_converts_what_the_server_returned()
        {
            var connection = new RecordingDbConnection { ScalarResult = 42UL };
            CommandBuilderFactory.Register(new TestProviderCommandBuilder());

            var id = await connection.Insert<TpIdentityRow, int>(new TpIdentityRow { Label = "a" });

            Assert.Equal(42, id);
        }

        [Fact]
        public async Task Insert_with_a_typed_result_is_the_default_when_the_server_returned_nothing()
        {
            var connection = new RecordingDbConnection();
            CommandBuilderFactory.Register(new TestProviderCommandBuilder());

            var id = await connection.Insert<TpIdentityRow, int?>(new TpIdentityRow { Label = "a" });

            Assert.Null(id);
        }
    }
}

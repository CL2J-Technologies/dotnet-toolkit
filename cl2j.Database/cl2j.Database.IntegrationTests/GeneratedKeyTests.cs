using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Insert ends in ExecuteScalarAsync and returns what it gets, and Insert&lt;TIn, TOut&gt;
    ///     exists to hand that value back typed. A plain INSERT ... VALUES produces no result set,
    ///     so both returned nothing — always null, always default. The one thing a caller wants
    ///     after inserting a row with a generated key was the one thing they could not get.
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class GeneratedKeyTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(IdentityKeyedRow), CancellationToken.None);
            await connection.CreateTable<IdentityKeyedRow>();
        }

        public async Task DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(IdentityKeyedRow), CancellationToken.None);
        }

        [Fact]
        public async Task Insert_hands_back_the_identity_the_server_picked()
        {
            await using var connection = await fixture.OpenAsync();

            var id = await connection.Insert<IdentityKeyedRow, int>(new IdentityKeyedRow { Label = "first" });

            Assert.True(id > 0);

            var read = await connection.QueryKey<IdentityKeyedRow>(id);
            Assert.Equal("first", read!.Label);
        }

        [Fact]
        public async Task Two_inserts_hand_back_two_different_identities()
        {
            await using var connection = await fixture.OpenAsync();

            var first = await connection.Insert<IdentityKeyedRow, int>(new IdentityKeyedRow { Label = "a" });
            var second = await connection.Insert<IdentityKeyedRow, int>(new IdentityKeyedRow { Label = "b" });

            Assert.NotEqual(first, second);
        }

        [Fact]
        public async Task A_row_with_a_key_of_its_own_still_inserts_and_returns_nothing_to_generate()
        {
            //Nothing is generated for a string key, so there is nothing to hand back — and the
            //statement must not ask for an identity that does not exist.
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(PlainKeyedRow), CancellationToken.None);
            await connection.CreateTable<PlainKeyedRow>();
            try
            {
                await connection.Insert(new PlainKeyedRow { Code = "chosen", Label = "x" });

                var read = await connection.QueryKey<PlainKeyedRow>("chosen");
                Assert.Equal("x", read!.Label);
            }
            finally
            {
                await connection.DropTableIfExists(typeof(PlainKeyedRow), CancellationToken.None);
            }
        }
    }
}

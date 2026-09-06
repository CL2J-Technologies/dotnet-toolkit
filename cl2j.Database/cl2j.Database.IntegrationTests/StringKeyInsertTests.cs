using Microsoft.Data.SqlClient;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     A trap in the annotations, found by these tests rather than by reading.
    ///
    ///     <para>
    ///     <c>GetInsertStatement</c> builds its column list from
    ///     <c>Where(c =&gt; c.ColumnAtribute.Key != KeyType.Key)</c>. That is right for an
    ///     <c>int IDENTITY</c> column, where the server supplies the value — and wrong for a string
    ///     natural key, where nobody does. The value is dropped from the INSERT and the NOT NULL
    ///     primary key rejects the row.
    ///     </para>
    ///
    ///     <para>
    ///     <c>KeyType.Key</c> is the obvious annotation to reach for on a primary key, so the
    ///     failure lands on the most natural reading of the API. The message names the column, not
    ///     the annotation, so the trail back to the cause is not short. Characterised here rather
    ///     than fixed: making Insert include string keys changes what every existing consumer
    ///     writes, which is not a decision to slip into a test pull request.
    ///     </para>
    /// </summary>
    [Collection(SqlServerCollection.Name)]
    public class StringKeyInsertTests(SqlServerFixture fixture) : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(PlainKeyedRow), CancellationToken.None);
            await connection.CreateTable<PlainKeyedRow>();
        }

        public async Task DisposeAsync()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(PlainKeyedRow), CancellationToken.None);
        }

        [Fact]
        public async Task A_string_key_declared_as_Key_is_written_like_any_other_column()
        {
            await using var connection = await fixture.OpenAsync();

            await connection.Insert(new PlainKeyedRow { Code = "abc", Label = "anything" });

            var read = await connection.QueryKey<PlainKeyedRow>("abc");
            Assert.Equal("anything", read!.Label);
        }

        [Fact]
        public async Task An_int_identity_key_is_still_left_to_the_server()
        {
            //The other half of the rule, and the reason the exclusion existed. An int key is
            //declared IDENTITY(1,1), so sending a value for it is an error rather than a courtesy.
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(IdentityKeyedRow), CancellationToken.None);
            await connection.CreateTable<IdentityKeyedRow>();
            try
            {
                await connection.Insert(new IdentityKeyedRow { Label = "server picks the id" });

                var rows = await connection.Query<IdentityKeyedRow>("SELECT [Id],[Label] FROM [IdentityKeyed]");

                var row = Assert.Single(rows);
                Assert.True(row.Id > 0);
            }
            finally
            {
                await connection.DropTableIfExists(typeof(IdentityKeyedRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task The_same_shape_works_when_the_key_is_self_generated()
        {
            //The workaround, and the reason the other test classes here use SelfGeneratedKey.
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(AnsiKeyedRow), CancellationToken.None);
            await connection.CreateTable<AnsiKeyedRow>();
            try
            {
                await connection.Insert(new AnsiKeyedRow { Code = "abc", Label = "anything" });

                var read = await connection.QueryKey<AnsiKeyedRow>("abc");
                Assert.Equal("anything", read!.Label);
            }
            finally
            {
                await connection.DropTableIfExists(typeof(AnsiKeyedRow), CancellationToken.None);
            }
        }
    }
}

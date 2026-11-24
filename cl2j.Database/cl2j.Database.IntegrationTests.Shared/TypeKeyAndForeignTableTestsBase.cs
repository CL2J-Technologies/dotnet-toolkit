using cl2j.Database.DataAnnotations;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Keys of each type that makes sense as one, read by key, by keys and by example; and a
    ///     nested public class, which the generated reader has to resolve by a name that is not
    ///     <c>Namespace.Name</c>.
    ///
    ///     <para>
    ///     The half of the original class that reads tables the library did not create stays with
    ///     SQL Server (see <c>TypeKeyAndForeignTableTests</c> in its test project): it is raw SQL
    ///     casts to SQL Server's column types and the exact conversions of SqlClient's getters,
    ///     neither of which a provider shares. A provider writes the equivalent against its own
    ///     types. No hook is needed here: the key tests use only the library's own DDL, and a Guid
    ///     key is the server's to generate on every provider.
    ///     </para>
    /// </summary>
    public abstract class TypeKeyAndForeignTableTestsBase(IDatabaseFixture fixture)
    {
        /// <summary>The fixture the derived class was built with.</summary>
        protected IDatabaseFixture Fixture => fixture;

        [Fact]
        public async Task An_int_key_is_read_by_key_by_keys_and_by_example()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(IdentityKeyedRow), CancellationToken.None);
            await connection.CreateTable<IdentityKeyedRow>();
            try
            {
                var first = await connection.Insert<IdentityKeyedRow, int>(new IdentityKeyedRow { Label = "one" });
                var second = await connection.Insert<IdentityKeyedRow, int>(new IdentityKeyedRow { Label = "two" });

                Assert.Equal("two", (await connection.QueryKey<IdentityKeyedRow>(second))!.Label);
                Assert.Equal("one", Assert.Single(await connection.Query<IdentityKeyedRow>(new { Id = first })).Label);
                Assert.Equal(2, (await connection.QueryKeys<IdentityKeyedRow>([first, second])).Count);
                Assert.Null(await connection.QueryKey<IdentityKeyedRow>(second + 1));
            }
            finally
            {
                await connection.DropTableIfExists(typeof(IdentityKeyedRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task A_long_key_is_read_by_key_by_keys_and_by_example()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(LongKeyRow), CancellationToken.None);
            await connection.CreateTable<LongKeyRow>();
            try
            {
                var first = await connection.Insert<LongKeyRow, long>(new LongKeyRow { Label = "one" });
                var second = await connection.Insert<LongKeyRow, long>(new LongKeyRow { Label = "two" });

                Assert.Equal("two", (await connection.QueryKey<LongKeyRow>(second))!.Label);
                Assert.Equal("one", Assert.Single(await connection.Query<LongKeyRow>(new { Id = first })).Label);
                Assert.Equal(2, (await connection.QueryKeys<LongKeyRow>([first, second])).Count);
            }
            finally
            {
                await connection.DropTableIfExists(typeof(LongKeyRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task A_Guid_key_is_read_by_key_by_keys_and_by_example()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(GuidKeyRow), CancellationToken.None);
            await connection.CreateTable<GuidKeyRow>();
            try
            {
                await connection.Insert(new GuidKeyRow { Label = "one" });
                await connection.Insert(new GuidKeyRow { Label = "two" });
                var rows = await connection.Query<GuidKeyRow>();
                var two = rows.Single(r => r.Label == "two").Id;
                var one = rows.Single(r => r.Label == "one").Id;

                Assert.Equal("two", (await connection.QueryKey<GuidKeyRow>(two))!.Label);
                Assert.Equal("one", Assert.Single(await connection.Query<GuidKeyRow>(new { Id = one })).Label);
                Assert.Equal(2, (await connection.QueryKeys<GuidKeyRow>([one, two])).Count);
                Assert.Null(await connection.QueryKey<GuidKeyRow>(Guid.NewGuid()));
            }
            finally
            {
                await connection.DropTableIfExists(typeof(GuidKeyRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task A_string_key_is_read_by_key_by_keys_and_by_example()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(PlainKeyedRow), CancellationToken.None);
            await connection.CreateTable<PlainKeyedRow>();
            try
            {
                await connection.Insert(new PlainKeyedRow { Code = "a-1", Label = "one" });
                await connection.BulkInsert(new[] { new PlainKeyedRow { Code = "b-2", Label = "two" } });

                Assert.Equal("one", (await connection.QueryKey<PlainKeyedRow>("a-1"))!.Label);
                Assert.Equal("two", Assert.Single(await connection.Query<PlainKeyedRow>(new { Code = "b-2" })).Label);
                Assert.Equal(2, (await connection.QueryKeys<PlainKeyedRow>(["a-1", "b-2"])).Count);
                Assert.Null(await connection.QueryKey<PlainKeyedRow>("c-3"));
            }
            finally
            {
                await connection.DropTableIfExists(typeof(PlainKeyedRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task A_Guid_key_the_caller_supplies_is_not_what_the_server_stores()
        {
            //Pinned: a Guid key is left to the server (DEFAULT NEWID()), for Insert and BulkInsert
            //alike, so a Guid set on the property is ignored. Nothing fails, which is the reason to
            //have the test.
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(GuidKeyRow), CancellationToken.None);
            await connection.CreateTable<GuidKeyRow>();
            try
            {
                var supplied = Guid.NewGuid();

                await connection.Insert(new GuidKeyRow { Id = supplied, Label = "x" });

                Assert.NotEqual(supplied, Assert.Single(await connection.Query<GuidKeyRow>()).Id);
            }
            finally
            {
                await connection.DropTableIfExists(typeof(GuidKeyRow), CancellationToken.None);
            }
        }

        [Fact]
        public async Task A_nested_public_class_is_read_like_any_other()
        {
            //The generated reader resolves the entity by its name, and a nested class is not
            //Namespace.Name: this is the case that failed while KeyAndJsonFixTests was written.
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(NestedRow), CancellationToken.None);
            await connection.CreateTable<NestedRow>();
            try
            {
                await connection.Insert(new NestedRow { Label = "nested" });

                Assert.Equal("nested", Assert.Single(await connection.Query<NestedRow>()).Label);
            }
            finally
            {
                await connection.DropTableIfExists(typeof(NestedRow), CancellationToken.None);
            }
        }

        [Table("NestedRow")]
        public sealed class NestedRow
        {
            [Column(Name = "Id", Key = KeyType.Key)]
            public int Id { get; set; }

            [Column(Name = "Label", Length = 20)]
            public string Label { get; set; } = string.Empty;
        }
    }
}

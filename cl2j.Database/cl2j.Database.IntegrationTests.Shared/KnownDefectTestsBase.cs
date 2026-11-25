using cl2j.Database.DataAnnotations;
using Xunit;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     Defects that are filed and not fixed here, and that reach a server. Each test states
    ///     the behaviour the issue asks for and is skipped until the issue is closed; remove the
    ///     <c>Skip</c> with the fix.
    ///
    ///     <para>
    ///     No hooks: the <c>DateOnly</c> test asks for the same value back on every provider.
    ///     </para>
    /// </summary>
    public abstract class KnownDefectTestsBase(IDatabaseFixture fixture)
    {
        [Fact]
        public async Task A_DateOnly_survives_the_write_read_cycle()
        {
            await using var connection = await fixture.OpenAsync();
            await connection.DropTableIfExists(typeof(DateOnlyRow), CancellationToken.None);
            await connection.CreateTable<DateOnlyRow>();
            try
            {
                var id = await connection.NewKey<DateOnlyRow>();
                await connection.Insert(new DateOnlyRow { Id = id, Day = new DateOnly(2026, 9, 27) });

                //The insert succeeds, and an empty table would hide the failure below: it takes a
                //row coming back to throw.
                var read = await connection.QueryKey<DateOnlyRow>(id);

                Assert.Equal(new DateOnly(2026, 9, 27), read!.Day);
            }
            finally
            {
                await connection.DropTableIfExists(typeof(DateOnlyRow), CancellationToken.None);
            }
        }

    }

    /// <summary>
    ///     A <see cref="DateOnly"/> mapped to a <c>date</c> column. See issue #46.
    /// </summary>
    [Table("DateOnlyShapes")]
    public sealed class DateOnlyRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Day")]
        public DateOnly Day { get; set; }
    }
}

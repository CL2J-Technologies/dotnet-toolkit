using cl2j.Database.DataAnnotations;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     A <see cref="DateOnly"/> mapped to a <c>date</c> column. See issue #46. Only the SQL
    ///     Server known-defect tests use it, so it stays with them rather than in the shared
    ///     entities.
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

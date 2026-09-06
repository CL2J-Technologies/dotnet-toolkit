using cl2j.Database.DataAnnotations;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     A table with a single string key. This is the shape QueryKeys accepts: the builder
    ///     refuses any type that does not have exactly one key.
    /// </summary>
    [Table("Customer")]
    public sealed class Customer
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Name", Length = 100)]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    ///     A table whose key is declared Unicode by the schema. The ANSI binding rule must not
    ///     reach it either, or a write would drop the characters the column exists to hold.
    /// </summary>
    [Table("UnicodeKeyedRow")]
    public sealed class UnicodeKeyedRow
    {
        [Column(Name = "Slug", Key = KeyType.Key, TypeName = "nvarchar(100)")]
        public string Slug { get; set; } = string.Empty;
    }

    /// <summary>
    ///     A table whose key is not a string. The ANSI binding rule must not reach it.
    /// </summary>
    [Table("Counter")]
    public sealed class Counter
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "Label", Length = 50)]
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    ///     A table with a composite key. QueryKeys refuses it, but a read by key must know how to
    ///     join the columns.
    /// </summary>
    [Table("TenantScopedRow")]
    public sealed class TenantScopedRow
    {
        [Column(Name = "TenantId", Key = KeyType.Key, Length = 50)]
        public string TenantId { get; set; } = string.Empty;

        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Name", Length = 100)]
        public string Name { get; set; } = string.Empty;
    }
}

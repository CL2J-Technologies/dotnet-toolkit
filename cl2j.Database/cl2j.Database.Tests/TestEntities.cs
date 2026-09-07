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

    /// <summary>
    ///     A schema-qualified table with a foreign key, an ignored property, a required column and
    ///     a JSON one. Used by the CREATE TABLE test, which is where those all show up at once.
    /// </summary>
    [Table("Invoice", Schema = "billing")]
    public sealed class Invoice
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "CustomerId", Length = 50)]
        [ForeignKey("FK_Invoice_Customer", "Customer", "Id")]
        public string CustomerId { get; set; } = string.Empty;

        [Column(Name = "Amount", Length = 18, Decimals = 2, Required = true)]
        public decimal Amount { get; set; }

        [Column(Name = "Metadata", Json = true)]
        public string? Metadata { get; set; }

        [Ignore]
        public string NotAColumn { get; set; } = string.Empty;
    }

    /// <summary>
    ///     No <see cref="TableAttribute"/> and no declared key. The table name should fall back to
    ///     the type name, and a property called Id should be picked up as the key by convention.
    /// </summary>
    public sealed class ImplicitlyKeyed
    {
        public string Id { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    ///     One property per branch of GetColumnDataType. The values are never read; only the
    ///     declared types matter.
    /// </summary>
    [Table("AllTypes")]
    public sealed class AllTypesRow
    {
        [Column(Name = "Flag")]
        public bool Flag { get; set; }

        [Column(Name = "Small")]
        public short Small { get; set; }

        [Column(Name = "Count")]
        public int Count { get; set; }

        [Column(Name = "Big")]
        public long Big { get; set; }

        [Column(Name = "Price", Length = 18, Decimals = 2)]
        public decimal Price { get; set; }

        [Column(Name = "Ratio")]
        public double Ratio { get; set; }

        [Column(Name = "Rate")]
        public float Rate { get; set; }

        [Column(Name = "Bounded", Length = 64)]
        public string Bounded { get; set; } = string.Empty;

        [Column(Name = "Unbounded")]
        public string Unbounded { get; set; } = string.Empty;

        [Column(Name = "Moment")]
        public DateTimeOffset Moment { get; set; }

        [Column(Name = "Stamp")]
        public DateTime Stamp { get; set; }

        [Column(Name = "Ref")]
        public Guid Ref { get; set; }

        [Column(Name = "Kind")]
        public SampleKind Kind { get; set; }

        [Column(Name = "Declared", TypeName = "geography")]
        public string Declared { get; set; } = string.Empty;

        [Column(Name = "WithDefault", Length = 10, Default = "'n/a'")]
        public string WithDefault { get; set; } = string.Empty;
    }

    /// <summary>
    ///     Nullable value types and a byte array. Every one of these used to fall through to the
    ///     varchar(MAX) default, because Nullable<T> is not the type it wraps. See issue #27.
    /// </summary>
    [Table("Nullables")]
    public sealed class NullablesRow
    {
        [Column(Name = "MaybeCount")]
        public int? MaybeCount { get; set; }

        [Column(Name = "MaybeWhen")]
        public DateTime? MaybeWhen { get; set; }

        [Column(Name = "MaybeMoment")]
        public DateTimeOffset? MaybeMoment { get; set; }

        [Column(Name = "MaybeRef")]
        public Guid? MaybeRef { get; set; }

        [Column(Name = "MaybeFlag")]
        public bool? MaybeFlag { get; set; }

        [Column(Name = "Payload")]
        public byte[]? Payload { get; set; }

        [Column(Name = "Elapsed")]
        public TimeSpan Elapsed { get; set; }

        [Column(Name = "Day")]
        public DateOnly Day { get; set; }

        [Column(Name = "Tiny")]
        public byte Tiny { get; set; }
    }

    /// <summary>
    ///     A decimal with no precision declared. SQL Server reads bare <c>decimal</c> as
    ///     <c>decimal(18,0)</c>, so the fraction goes away on the way in.
    /// </summary>
    [Table("Imprecise")]
    public sealed class ImpreciseRow
    {
        [Column(Name = "Amount")]
        public decimal Amount { get; set; }
    }

    /// <summary>
    ///     A type no provider can map. It must be refused rather than quietly stored as text.
    /// </summary>
    [Table("Unmappable")]
    public sealed class UnmappableRow
    {
        [Column(Name = "Thing")]
        public Uri? Thing { get; set; }
    }

    public enum SampleKind
    {
        First,
        Second
    }

    /// <summary>
    ///     A key whose type no provider knows how to declare. GetColumnKeyType must refuse it
    ///     rather than emit something meaningless.
    /// </summary>
    [Table("UnsupportedKeyRow")]
    public sealed class UnsupportedKeyRow
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public bool Id { get; set; }
    }

    /// <summary>
    ///     A key the library generates itself, with a prefix.
    /// </summary>
    [Table("PrefixedRow")]
    public sealed class PrefixedRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50, KeyPrefix = "cus_")]
        public string Id { get; set; } = string.Empty;
    }

    /// <summary>
    ///     Query parameters, not a table. GetQueryStatement(type, paramType) builds its WHERE from
    ///     the columns of a second type like this one.
    /// </summary>
    public sealed class CustomerFilter
    {
        [Column(Name = "Name")]
        public string Name { get; set; } = string.Empty;
    }
}

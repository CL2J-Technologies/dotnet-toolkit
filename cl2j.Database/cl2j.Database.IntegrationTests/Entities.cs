using cl2j.Database.DataAnnotations;

namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     One entity type per concern. Descriptors are cached by type for the life of the
    ///     process, and the table name comes from the type, so two test classes sharing a type
    ///     would share a table and interfere.
    /// </summary>
    [Table("RoundTrip")]
    public sealed class RoundTripRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Name", Length = 100)]
        public string Name { get; set; } = string.Empty;

        [Column(Name = "Quantity")]
        public int Quantity { get; set; }
    }

    [Table("KeyBatch")]
    public sealed class KeyBatchRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Position")]
        public int Position { get; set; }
    }

    /// <summary>
    ///     The types that make materialisation interesting: nullable, temporal, Guid and JSON.
    /// </summary>
    [Table("ReaderShapes")]
    public sealed class ReaderShapesRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Text", Length = 100)]
        public string? Text { get; set; }

        [Column(Name = "Count")]
        public int Count { get; set; }

        [Column(Name = "Flag")]
        public bool Flag { get; set; }

        [Column(Name = "Moment")]
        public DateTimeOffset Moment { get; set; }

        [Column(Name = "Payload", Json = true)]
        public string? Payload { get; set; }
    }

    /// <summary>
    ///     Two plain text columns of the same shape. Used to show what ordinal binding does when
    ///     nothing about the types gives the mismatch away.
    /// </summary>
    [Table("TwoTexts")]
    public sealed class TwoTextsRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "First", Length = 50)]
        public string? First { get; set; }

        [Column(Name = "Second", Length = 50)]
        public string? Second { get; set; }
    }

    [Table("Transacted")]
    public sealed class TransactedRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Label", Length = 50)]
        public string Label { get; set; } = string.Empty;
    }

    [Table("Bulk")]
    public sealed class BulkRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Position")]
        public int Position { get; set; }
    }

    /// <summary>
    ///     A varchar key, which is the shape issue #7 is about: a Unicode parameter compared
    ///     against it converts the column instead of the parameter, and the index stops being
    ///     usable.
    /// </summary>
    [Table("AnsiKeyed")]
    public sealed class AnsiKeyedRow
    {
        //SelfGeneratedKey rather than Key, and not by preference: Insert excludes KeyType.Key
        //columns from the column list, so a string key declared that way is never written. See
        //StringKeyInsertTests.
        [Column(Name = "Code", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Code { get; set; } = string.Empty;

        [Column(Name = "Label", Length = 50)]
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    ///     A string primary key declared <see cref="KeyType.Key"/>, which is the natural reading of
    ///     the annotation and does not work. Kept to hold that behaviour in place.
    /// </summary>
    [Table("PlainKeyed")]
    public sealed class PlainKeyedRow
    {
        [Column(Name = "Code", Key = KeyType.Key, Length = 50)]
        public string Code { get; set; } = string.Empty;

        [Column(Name = "Label", Length = 50)]
        public string Label { get; set; } = string.Empty;
    }
}

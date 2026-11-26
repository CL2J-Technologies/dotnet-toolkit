using cl2j.Database.DataAnnotations;

namespace cl2j.Database.MySql.IntegrationTests
{
    //Top-level public classes: the generated reader is compiled outside this assembly and cannot
    //see a nested or non-public type. Every table name starts with "My_", which the shared suite's
    //tables never do, so the two can share the server.

    /// <summary>A string key, to see which keys the default collation treats as one.</summary>
    [Table("My_Keyed")]
    public sealed class MyKeyedRow
    {
        [Column(Name = "Code", Key = KeyType.Key, Length = 50)]
        public string Code { get; set; } = string.Empty;

        [Column(Name = "Label", Length = 50)]
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>Two <c>varchar(10000)</c> columns: 80,000 bytes in utf8mb4, over the 65,535-byte row limit.</summary>
    [Table("My_TooWide")]
    public sealed class MyTooWideRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 20)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "First", Length = 10000)]
        public string? First { get; set; }

        [Column(Name = "Second", Length = 10000)]
        public string? Second { get; set; }
    }

    /// <summary>One <c>varchar(20000)</c> column: over the 16,383-character limit of a single utf8mb4 varchar.</summary>
    [Table("My_TooLong")]
    public sealed class MyTooLongColumnRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 20)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Body", Length = 20000)]
        public string? Body { get; set; }
    }

    /// <summary>Two <c>varchar(10000)</c> columns would not fit; four unbounded strings (longtext) do.</summary>
    [Table("My_Unbounded")]
    public sealed class MyUnboundedRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 20)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "First")]
        public string? First { get; set; }

        [Column(Name = "Second")]
        public string? Second { get; set; }

        [Column(Name = "Third")]
        public string? Third { get; set; }

        [Column(Name = "Fourth")]
        public string? Fourth { get; set; }
    }

    /// <summary>A <c>varchar(5)</c> column, for a value that does not fit.</summary>
    [Table("My_Short")]
    public sealed class MyShortRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 10)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Label", Length = 5)]
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>A key and a position: the rows of the bulk insert tests.</summary>
    [Table("My_Batch")]
    public sealed class MyBatchRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 40)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Position")]
        public int Position { get; set; }
    }

    /// <summary>A property of a type the provider maps to no column, and says no TypeName for.</summary>
    [Table("My_Unmapped")]
    public sealed class MyUnmappedRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 40)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Tags")]
        public int[] Tags { get; set; } = [];
    }

    /// <summary>The temporal columns of the offset, precision and range tests.</summary>
    [Table("My_Temporal")]
    public sealed class MyTemporalRow
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 40)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Moment")]
        public DateTimeOffset Moment { get; set; }

        [Column(Name = "At")]
        public DateTime At { get; set; }

        [Column(Name = "Day")]
        public DateOnly Day { get; set; }

        [Column(Name = "Clock")]
        public TimeOnly Clock { get; set; }
    }
}

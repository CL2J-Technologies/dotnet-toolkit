using cl2j.Database.DataAnnotations;

namespace cl2j.Database.PostgreSql.IntegrationTests
{
    //Top-level public classes: the generated reader compiles against them.

    public sealed class PgDocument
    {
        public string Kind { get; set; } = string.Empty;

        public List<int> Values { get; set; } = [];
    }

    [Table("PgJson")]
    public sealed class PgJsonRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Details", Json = true)]
        public PgDocument? Details { get; set; }

        //A length turns the JSON column into a varchar, bound as text.
        [Column(Name = "Capped", Json = true, Length = 200)]
        public PgDocument? Capped { get; set; }
    }

    [Table("PgMoments")]
    public sealed class PgMomentRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Stamp")]
        public DateTime Stamp { get; set; } = new DateTime(2000, 1, 1);

        [Column(Name = "Moment")]
        public DateTimeOffset Moment { get; set; } = DateTimeOffset.UnixEpoch;
    }

    [Table("PgShort")]
    public sealed class PgShortRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Code", Length = 5)]
        public string Code { get; set; } = string.Empty;

        [Column(Name = "Free")]
        public string? Free { get; set; }
    }

    [Table("PgIntKey")]
    public sealed class PgIntKeyRow
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "Label", Length = 20)]
        public string Label { get; set; } = string.Empty;
    }

    [Table("PgLongKey")]
    public sealed class PgLongKeyRow
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public long Id { get; set; }

        [Column(Name = "Label", Length = 20)]
        public string Label { get; set; } = string.Empty;
    }

    [Table("PgGuidKey")]
    public sealed class PgGuidKeyRow
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public Guid Id { get; set; }

        [Column(Name = "Label", Length = 20)]
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>Every kind of column the COPY writer picks a type for, and two it leaves to Npgsql.</summary>
    [Table("PgCopy")]
    public sealed class PgCopyRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Label", Length = 20)]
        public string Label { get; set; } = string.Empty;

        [Column(Name = "Details", Json = true)]
        public PgDocument? Details { get; set; }

        [Column(Name = "Stamp")]
        public DateTime Stamp { get; set; } = new DateTime(2000, 1, 1);

        [Column(Name = "Moment")]
        public DateTimeOffset Moment { get; set; } = DateTimeOffset.UnixEpoch;

        //TypeName leaves the choice of the wire type to Npgsql, with the DateTime and
        //DateTimeOffset corrections.
        [Column(Name = "DeclaredStamp", TypeName = "timestamp")]
        public DateTime DeclaredStamp { get; set; } = new DateTime(2000, 1, 1);

        [Column(Name = "DeclaredMoment", TypeName = "timestamptz")]
        public DateTimeOffset DeclaredMoment { get; set; } = DateTimeOffset.UnixEpoch;

        [Column(Name = "DeclaredJson", Json = true, TypeName = "jsonb")]
        public PgDocument? DeclaredJson { get; set; }

        [Column(Name = "DeclaredPlainJson", Json = true, TypeName = "json")]
        public PgDocument? DeclaredPlainJson { get; set; }

        [Column(Name = "Capped", Json = true, Length = 200)]
        public PgDocument? Capped { get; set; }
    }

    [Table("PgSchemed", Schema = "pgother")]
    public sealed class PgSchemedRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Id { get; set; } = string.Empty;
    }

    /// <summary>
    ///     A reserved word as a column name, and a double quote in the table name. (A quote in a
    ///     column name would also end up in the parameter name, which the core does not escape.)
    /// </summary>
    [Table("Pg\"Odd Name")]
    public sealed class PgOddRow
    {
        [Column(Name = "Id", Key = KeyType.SelfGeneratedKey, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "select")]
        public int Select { get; set; }

        [Column(Name = "MixedCase")]
        public string? MixedCase { get; set; }
    }
}

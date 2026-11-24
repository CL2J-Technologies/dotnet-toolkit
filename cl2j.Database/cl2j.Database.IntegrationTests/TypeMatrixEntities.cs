using cl2j.Database.DataAnnotations;

namespace cl2j.Database.TypeMatrix
{
    /// <summary>
    ///     A value that is not defined in <see cref="MatrixKind"/>, an enum column being an int.
    /// </summary>
    public enum MatrixKind
    {
        Zero = 0,
        One = 1,
        Big = 1000
    }

    //One entity per mapped type, so that a failure names the type. Each has the three shapes the
    //generated reader branches on: V is neither key nor required (read through GetValue and a
    //cast), R is Required (read through a typed getter where there is one), N is nullable.
    //Generated once and then edited by hand; the entities are shared by the unit and the
    //integration test projects.

    [Table("Mx_Bool")]
    public sealed class MxBool
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public bool V { get; set; }

        [Column(Name = "R", Required = true)]
        public bool R { get; set; }

        [Column(Name = "N")]
        public bool? N { get; set; }
    }

    [Table("Mx_Byte")]
    public sealed class MxByte
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public byte V { get; set; }

        [Column(Name = "R", Required = true)]
        public byte R { get; set; }

        [Column(Name = "N")]
        public byte? N { get; set; }
    }

    [Table("Mx_Short")]
    public sealed class MxShort
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public short V { get; set; }

        [Column(Name = "R", Required = true)]
        public short R { get; set; }

        [Column(Name = "N")]
        public short? N { get; set; }
    }

    [Table("Mx_Int")]
    public sealed class MxInt
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public int V { get; set; }

        [Column(Name = "R", Required = true)]
        public int R { get; set; }

        [Column(Name = "N")]
        public int? N { get; set; }
    }

    [Table("Mx_Long")]
    public sealed class MxLong
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public long V { get; set; }

        [Column(Name = "R", Required = true)]
        public long R { get; set; }

        [Column(Name = "N")]
        public long? N { get; set; }
    }

    [Table("Mx_Float")]
    public sealed class MxFloat
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public float V { get; set; }

        [Column(Name = "R", Required = true)]
        public float R { get; set; }

        [Column(Name = "N")]
        public float? N { get; set; }
    }

    [Table("Mx_Double")]
    public sealed class MxDouble
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public double V { get; set; }

        [Column(Name = "R", Required = true)]
        public double R { get; set; }

        [Column(Name = "N")]
        public double? N { get; set; }
    }

    [Table("Mx_Decimal")]
    public sealed class MxDecimal
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V", Length = 18, Decimals = 2)]
        public decimal V { get; set; }

        [Column(Name = "R", Required = true, Length = 18, Decimals = 2)]
        public decimal R { get; set; }

        [Column(Name = "N", Length = 18, Decimals = 2)]
        public decimal? N { get; set; }
    }

    [Table("Mx_Guid")]
    public sealed class MxGuid
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public Guid V { get; set; }

        [Column(Name = "R", Required = true)]
        public Guid R { get; set; }

        [Column(Name = "N")]
        public Guid? N { get; set; }
    }

    [Table("Mx_String")]
    public sealed class MxString
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V", Length = 100)]
        public string? V { get; set; }

        [Column(Name = "R", Required = true, Length = 100)]
        public string R { get; set; } = string.Empty;

        [Column(Name = "N", Length = 100)]
        public string? N { get; set; }
    }

    [Table("Mx_Bytes")]
    public sealed class MxBytes
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public byte[]? V { get; set; }

        [Column(Name = "R", Required = true)]
        public byte[] R { get; set; } = [];

        [Column(Name = "N")]
        public byte[]? N { get; set; }
    }

    [Table("Mx_DateTime")]
    public sealed class MxDateTime
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public DateTime V { get; set; }

        [Column(Name = "R", Required = true)]
        public DateTime R { get; set; }

        [Column(Name = "N")]
        public DateTime? N { get; set; }
    }

    [Table("Mx_DateTimeOffset")]
    public sealed class MxDateTimeOffset
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public DateTimeOffset V { get; set; }

        [Column(Name = "R", Required = true)]
        public DateTimeOffset R { get; set; }

        [Column(Name = "N")]
        public DateTimeOffset? N { get; set; }
    }

    [Table("Mx_DateOnly")]
    public sealed class MxDateOnly
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public DateOnly V { get; set; }

        [Column(Name = "R", Required = true)]
        public DateOnly R { get; set; }

        [Column(Name = "N")]
        public DateOnly? N { get; set; }
    }

    [Table("Mx_TimeOnly")]
    public sealed class MxTimeOnly
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public TimeOnly V { get; set; }

        [Column(Name = "R", Required = true)]
        public TimeOnly R { get; set; }

        [Column(Name = "N")]
        public TimeOnly? N { get; set; }
    }

    [Table("Mx_TimeSpan")]
    public sealed class MxTimeSpan
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public TimeSpan V { get; set; }

        [Column(Name = "R", Required = true)]
        public TimeSpan R { get; set; }

        [Column(Name = "N")]
        public TimeSpan? N { get; set; }
    }

    [Table("Mx_Char")]
    public sealed class MxChar
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public char V { get; set; }

        [Column(Name = "R", Required = true)]
        public char R { get; set; }

        [Column(Name = "N")]
        public char? N { get; set; }
    }

    [Table("Mx_Enum")]
    public sealed class MxEnum
    {
        [Column(Name = "Id", Key = KeyType.Key)]
        public int Id { get; set; }

        [Column(Name = "V")]
        public MatrixKind V { get; set; }

        [Column(Name = "R", Required = true)]
        public MatrixKind R { get; set; }

        [Column(Name = "N")]
        public MatrixKind? N { get; set; }
    }
}

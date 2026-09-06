using cl2j.Database.DataAnnotations;

namespace cl2j.Database.Tests
{
    /// <summary>
    ///     Une table a cle unique de type string. C est la forme que QueryKeys accepte : le builder
    ///     refuse tout type qui n a pas exactement une cle.
    /// </summary>
    [Table("Customer")]
    public sealed class Customer
    {
        [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
        public string Id { get; set; } = string.Empty;

        [Column(Name = "Name", Length = 100)]
        public string Name { get; set; } = string.Empty;
    }
}

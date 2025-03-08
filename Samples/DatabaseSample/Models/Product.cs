using cl2j.Database.DataAnnotations;

namespace DatabaseSample.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Column(Length = 50, Required = true)]
        public string Name { get; set; } = null!;

        [Column(Length = 500, Required = true, Json = true)]
        public Dictionary<string, string> Display { get; set; } = [];

        [Column(Length = 36, Required = true)]
        [ForeignKey("FK_Product_CategoryId", "Cat", "CategoryId")]
        public string CategoryId { get; set; } = null!;

        [Column(Length = 8, Decimals = 2, Required = true)]
        public decimal Price { get; set; }

        [Column(Required = true)]
        public bool Active { get; set; }

        [Column(Default = null)]
        public DateTimeOffset CreatedOn { get; set; }

        [Column(Length = 200)]
        public string? Tips { get; set; }

        public override string ToString()
        {
            return $"{Id} [Name={Name}, Display={Display}, CategoryId={CategoryId}, Price={Price}, Active={Active}, CreatedOn={CreatedOn}]";
        }
    }
}

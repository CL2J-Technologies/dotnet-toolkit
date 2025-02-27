using System.ComponentModel.DataAnnotations;
using cl2j.Database.DataAnnotations;
using cl2j.Tooling;

namespace DatabaseSample.Models
{
    internal class Product
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = null!;

        [Required]
        [JsonDataType()]
        public Localized<string> Display { get; set; } = [];

        [Required]
        [ForeignKey("FK_Product_CategoryId", "Category", "CategoryId")]
        [MaxLength(36)]
        public string CategoryId { get; set; } = null!;

        [Required]
        [Precision(8, 2)]
        public decimal Price { get; set; }

        [Required]
        public bool Active { get; set; }

        public DateTimeOffset CreatedOn { get; set; }

        [MaxLength(50)]
        public string? CreatedBy { get; set; }
    }
}

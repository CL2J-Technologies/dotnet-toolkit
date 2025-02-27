using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DatabaseSample.Models
{
    [Table("Cat")]
    internal record Category
    {
        [Key]
        public int CategoryId { get; set; }

        public string Name { get; set; } = null!;
    }
}

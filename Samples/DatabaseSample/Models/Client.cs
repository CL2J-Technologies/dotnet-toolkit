using cl2j.Database.DataAnnotations;

namespace DatabaseSample.Models
{
    public class Client
    {
        public int Id { get; set; }

        [Column(Length = 50, Required = true)]
        public string Name { get; set; } = null!;

        [Column(Length = 8, Decimals = 2, Required = true)]
        public decimal Balance { get; set; }

        [Column(Required = true)]
        public bool Active { get; set; }

        public DateTimeOffset CreatedOn { get; set; }
    }
}

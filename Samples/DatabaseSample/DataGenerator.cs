using DatabaseSample.Models;

namespace DatabaseSample
{
    internal static class DataGenerator
    {
        internal static List<Category> GenerateCategories()
        {
            var categories = new List<Category>();
            for (var i = 1; i <= 10; ++i)
                categories.Add(new Category { Name = $"Category{i}" });
            return categories;
        }

        internal static List<Product> GenerateProducts(List<Category> categories)
        {
            var products = new List<Product>();
            for (var i = 1; i <= 50; ++i)
            {
                products.Add(new Product
                {
                    Name = $"Product{i}",
                    Display = new Dictionary<string, string> { { "fr", $"Nom du produit {i}" }, { "en", $"Product {i} name" } },
                    CategoryId = categories[(i - 1) % categories.Count].CategoryId,
                    Price = i * 10,
                    Active = i % 3 != 0
                });
            }
            return products;
        }

        internal static List<Client> GenerateClients()
        {
            var clients = new List<Client>();
            for (var i = 1; i <= 100000; ++i)
            {
                clients.Add(new Client
                {
                    Name = $"Product{i}",
                    Balance = i * 10,
                    Active = i % 3 != 0,
                    CreatedOn = DateTimeOffset.UtcNow
                });
            }
            return clients;
        }
    }
}

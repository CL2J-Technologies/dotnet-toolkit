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
            for (var i = 1; i <= 10; ++i)
            {
                products.Add(new Product
                {
                    Name = $"Product{i}",
                    Display = new Dictionary<string, string> { { "fr", $"Nom du produit {i}" }, { "en", $"Product {i} name" } },
                    CategoryId = categories[i - 1].CategoryId,
                    Price = i * 10,
                    Active = i % 3 != 0
                });
            }
            return products;
        }
    }
}

using System.Diagnostics;
using cl2j.Database;
using cl2j.Database.SqlServer;
using DatabaseSample;
using DatabaseSample.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#if DEBUG

#if true
var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddDebug().SetMinimumLevel(LogLevel.Trace);
    builder.AddConsole().SetMinimumLevel(LogLevel.Trace);
});
services.AddDatabase(register: o => SqlServer.Register());
var serviceProvider = services.BuildServiceProvider();
serviceProvider.UseDatabase();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
#else
SqlServer.Register();
#endif

var connectionString = "Data Source=.;Initial Catalog=DatabaseSample;Integrated Security=True;MultipleActiveResultSets=False;Connection Timeout=15;encrypt=true;trustServerCertificate=true";
using var connection = new SqlConnection(connectionString);

var executePrepare = true;
if (executePrepare)
{
    await connection.DropTableIfExists<Product>();
    await connection.DropTableIfExists<Category>();

    await connection.CreateTableIfRequired<Category>();
    await connection.CreateTableIfRequired<Product>();
    Console.WriteLine($"Dropped and created tables: Category, Product");

    var generatedCategories = DataGenerator.GenerateCategories();
    await connection.BulkInsert(generatedCategories);
    var categories = await connection.Query<Category>();
    Console.WriteLine($"Inserted {generatedCategories.Count} Categories");

    var generatedProducts = DataGenerator.GenerateProducts(categories);
    await connection.BulkInsert(generatedProducts);
    Console.WriteLine($"Inserted {generatedProducts.Count} Products");

    await connection.DropTableIfExists<Client>();
    await connection.CreateTableIfRequired<Client>();
    Console.WriteLine($"Dropped and created tables: Clients");

    var generatedClients = DataGenerator.GenerateClients(1_000_000);
    await connection.BulkInsert(generatedClients);
    Console.WriteLine($"BulkInsert {generatedClients.Count} Clients");
}

//Query from SQL
var clients = await connection.Query<Client>("SELECT [Id], [Name], [Balance], [Active], [CreatedOn] FROM [Client]");
Console.WriteLine($"{clients.Count} clients");

var products = await connection.Query<Product>("SELECT * FROM [Product] ORDER BY Id ASC");
Console.WriteLine($"{products.Count} products");

var product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=10");
Debug.Assert(product is not null);
Console.WriteLine($"Product {product.Id} retreived");

product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=@Id", new { Id = 10 });
Debug.Assert(product is not null);
Console.WriteLine($"Product {product.Id} retreived");

//Update a record
if (product is not null)
{
    product.Price = 1;
    await connection.Update(product);
    Console.WriteLine($"Updated product {product.Id}");

    product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=10");
    Debug.Assert(product?.Price == 1, "Price not changed --> {sw.ElapsedMilliseconds}ms");
}

//Delete a record
if (product is not null)
{
    await connection.Delete(product);
    product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=10");
    Console.WriteLine($"Deleted product 10");
    Debug.Assert(product is null, "Product 10 not deleted --> {sw.ElapsedMilliseconds}ms");

    await connection.DeleteKey<Product>(13);
    Console.WriteLine($"Deleted product 13");
    product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=13");
    Debug.Assert(product is null, "Product 13 not deleted --> {sw.ElapsedMilliseconds}ms");
}

//Execute a command
var res = await connection.Execute("UPDATE [Product] SET Price=@Price WHERE [Id]=@Id", new { Id = 14, Price = 10000 });
Debug.Assert(res == 1);
Console.WriteLine($"Updated product 14");

#else

var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(Program).Assembly);

#endif

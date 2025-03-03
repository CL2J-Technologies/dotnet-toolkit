using System.Diagnostics;
using cl2j.Database;
using cl2j.Logging;
using DatabaseSample;
using DatabaseSample.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddDebug().SetMinimumLevel(LogLevel.Trace);
    builder.AddConsole().SetMinimumLevel(LogLevel.Trace);
});
services.AddDatabase(o => { o.TraceLevel = LogLevel.None; });
var serviceProvider = services.BuildServiceProvider();
serviceProvider.UseDatabase();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

var connectionString = "Data Source=.;Initial Catalog=DatabaseSample;Integrated Security=True;MultipleActiveResultSets=False;Connection Timeout=15;encrypt=true;trustServerCertificate=true";
using var connection = new SqlConnection(connectionString);

var executePrepare = true;
if (executePrepare)
{
    await connection.DropTableIfExists<Product>();
    await connection.DropTableIfExists<Category>();

    await connection.CreateTableIfRequired<Category>();
    await connection.CreateTableIfRequired<Product>();

    var generatedCategories = DataGenerator.GenerateCategories();
    foreach (var category in generatedCategories)
        await connection.Insert(category);
    var generatedProducts = DataGenerator.GenerateProducts(generatedCategories);
    foreach (var p in generatedProducts)
        await connection.Insert(p);

    //await connection.DropTableIfExists<Client>();
    //await connection.CreateTableIfRequired<Client>();
    //var generatedClients = DataGenerator.GenerateClients();
    //foreach (var c in generatedClients)
    //    await connection.Insert(c);
}

#if DEBUG

//Query from SQL
var clients = await connection.Query<Client>("SELECT [Id], [Name], [Balance], [Active], [CreatedOn] FROM [Client]");
logger.LogTrace($"{clients.Count} clients");

var products = await connection.Query<Product>("SELECT * FROM [Product] ORDER BY Id ASC");
logger.LogTrace($"{products.Count} products");

var product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=10");
logger.LogTrace(product != null ? "Product retreived" : "No client retreived");

product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=@Id", new { Id = 10 });
logger.LogTrace(product != null ? "Product retreived" : "No client retreived");

//Update a record
if (product is not null)
{
    product.Price = 1;
    await connection.Update(product);
    product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=10");
    Debug.Assert(product?.Price == 1, "Price not changed.");
}

//Delete a record
if (product is not null)
{
    await connection.Delete(product);
    product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=10");
    Debug.Assert(product is null, "Product 10 not deleted.");

    await connection.DeleteKey<Product>(13);
    product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=13");
    Debug.Assert(product is null, "Product 13 not deleted.");
}

//Execute a command
if (await connection.Execute("UPDATE [Product] SET Price=@Price WHERE [Id]=@Id", new { Id = 14, Price = 10000 }) != 1)
    logger.LogError("Execute failed");

Console.WriteLine("Press any key");
Console.ReadKey();

#else

var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(Program).Assembly);

#endif

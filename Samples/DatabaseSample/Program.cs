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

var sw = Stopwatch.StartNew();

var executePrepare = true;
if (executePrepare)
{
    await connection.DropTableIfExists<Product>();
    await connection.DropTableIfExists<Category>();

    await connection.CreateTableIfRequired<Category>();
    await connection.CreateTableIfRequired<Product>();
    logger.LogTrace($"Dropped and created tables: Category, Product --> {sw.ElapsedMilliseconds}ms");
    sw.Restart();

    var generatedCategories = DataGenerator.GenerateCategories();
    await connection.InsertBatch(generatedCategories);
    var categories = await connection.Query<Category>();
    logger.LogTrace($"Inserted {generatedCategories.Count} Category --> {sw.ElapsedMilliseconds}ms");
    sw.Restart();

    var generatedProducts = DataGenerator.GenerateProducts(categories);
    await connection.InsertBatch(generatedProducts);
    logger.LogTrace($"Inserted {generatedProducts.Count} Product --> {sw.ElapsedMilliseconds}ms");
    sw.Restart();

    await connection.DropTableIfExists<Client>();
    await connection.CreateTableIfRequired<Client>();
    logger.LogTrace($"Dropped and created tables: Client --> {sw.ElapsedMilliseconds}ms");
    sw.Restart();

    var generatedClients = DataGenerator.GenerateClients(1_000_000);
    await connection.InsertBatch(generatedClients);
    logger.LogTrace($"Inserted Batch Client --> {sw.ElapsedMilliseconds}ms");
}

#if DEBUG

//Query from SQL
var clients = await connection.Query<Client>("SELECT [Id], [Name], [Balance], [Active], [CreatedOn] FROM [Client]");
logger.LogTrace($"{clients.Count} clients --> {sw.ElapsedMilliseconds}ms");
sw.Restart();

var products = await connection.Query<Product>("SELECT * FROM [Product] ORDER BY Id ASC");
logger.LogTrace($"{products.Count} products --> {sw.ElapsedMilliseconds}ms");
sw.Restart();

var product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=10");
logger.LogTrace($"{(product != null ? "Product retreived" : "No client retreived")} --> {sw.ElapsedMilliseconds}ms");
sw.Restart();

product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=@Id", new { Id = 10 });
logger.LogTrace($"{(product != null ? "Product retreived" : "No client retreived")} --> {sw.ElapsedMilliseconds}ms");
sw.Restart();

//Update a record
if (product is not null)
{
    product.Price = 1;
    await connection.Update(product);
    logger.LogTrace($"Updated product --> {sw.ElapsedMilliseconds}ms");
    sw.Restart();

    product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=10");
    Debug.Assert(product?.Price == 1, "Price not changed --> {sw.ElapsedMilliseconds}ms");
}
sw.Restart();

//Delete a record
if (product is not null)
{
    await connection.Delete(product);
    product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=10");
    logger.LogTrace($"Deleted product --> {sw.ElapsedMilliseconds}ms");
    Debug.Assert(product is null, "Product 10 not deleted --> {sw.ElapsedMilliseconds}ms");
    sw.Restart();

    await connection.DeleteKey<Product>(13);
    logger.LogTrace($"Deleted product --> {sw.ElapsedMilliseconds}ms");
    product = await connection.QuerySingle<Product>("SELECT * FROM [Product] WHERE [Id]=13");
    Debug.Assert(product is null, "Product 13 not deleted --> {sw.ElapsedMilliseconds}ms");
    sw.Restart();
}

//Execute a command
if (await connection.Execute("UPDATE [Product] SET Price=@Price WHERE [Id]=@Id", new { Id = 14, Price = 10000 }) != 1)
    logger.LogError("Execute failed");
logger.LogTrace($"Updated product --> {sw.ElapsedMilliseconds}ms");
sw.Restart();

Console.WriteLine("Press any key");
Console.ReadKey();

#else

var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(Program).Assembly);

#endif

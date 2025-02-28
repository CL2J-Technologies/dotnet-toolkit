using System.Diagnostics;
using cl2j.Database;
using cl2j.Logging;
using Dapper;
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

// ---------------------------------------------------------------------------------------------
using var connection = new SqlConnection(connectionString);

var executePrepare = false;

if (executePrepare)
{
    await connection.DropTableIfExists<Product>();
    await connection.DropTableIfExists<Category>();

    await connection.CreateTableIfRequired<Category>();
    await connection.CreateTableIfRequired<Product>();

    var categories = DataGenerator.GenerateCategories();
    foreach (var category in categories)
        await connection.Insert(category);
    var products = DataGenerator.GenerateProducts(categories);
    foreach (var product in products)
        await connection.Insert(product);


    await connection.DropTableIfExists<Client>();
    await connection.CreateTableIfRequired<Client>();
    var clients = DataGenerator.GenerateClients();
    foreach (var c in clients)
        await connection.Insert(c);
}

//Query from SQL
if (true)
{
    var results = await connection.Query<Client>("SELECT * FROM [Client] ORDER BY Id ASC");
    logger.LogTrace($"{results.Count}");
    results = await connection.Query<Client>();
    logger.LogTrace($"{results.Count}");

    var client = await connection.QuerySingle<Client>("SELECT * FROM [Client] WHERE [Id]=100");
}

//Dapper
await Benchmark("Query Clients", async () =>
{
    var results = await connection.QueryAsync<Client>("SELECT * FROM [Client] ORDER BY Id ASC");
    //logger.LogTrace($"{results.Count()}");
}, logger);


await Benchmark("Query Clients", async () =>
{
    var results = await connection.Query<Client>("SELECT * FROM [Client] ORDER BY Id ASC");
    //logger.LogTrace($"{results.Count}");
}, logger);

//var productsFromQuery = await connection.Query<Product>("SELECT * FROM [Product] ORDER BY Id ASC");
//foreach (var p in productsFromQuery)
//    logger.LogTrace(p.ToString());

//var productsFromQuery = await connection.QueryByKey<Product>(1);

static async Task Benchmark(string name, Func<Task> a, ILogger logger)
{
    var swTotal = Stopwatch.StartNew();
    var loops = 20;
    for (int i = 0; i < loops; ++i)
    {
        var sw = Stopwatch.StartNew();
        await a();
        //logger.LogInformation($"{name}: {sw.ElapsedMilliseconds}ms");
    }
    logger.LogInformation($"AVG {name}: {swTotal.ElapsedMilliseconds / loops}ms");
}
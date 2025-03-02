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

var executePrepare = false;
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
    foreach (var product in generatedProducts)
        await connection.Insert(product);

    await connection.DropTableIfExists<Client>();
    await connection.CreateTableIfRequired<Client>();
    var generatedClients = DataGenerator.GenerateClients();
    foreach (var c in generatedClients)
        await connection.Insert(c);
}

#if DEBUG

//Query from SQL
var client = await connection.QuerySingle<Client>("SELECT * FROM [Client] WHERE [Id]=100");
logger.LogTrace(client != null ? "Client retreived" : "No client retreived");

var clients = await connection.Query<Client>("SELECT [Id], [Name], [Balance], [Active], [CreatedOn] FROM [Client]");
logger.LogTrace($"{clients.Count} clients");

var products = await connection.Query<Product>("SELECT * FROM [Product] ORDER BY Id ASC");
logger.LogTrace($"{products.Count} products");

//Update a record
if (client is not null)
{
    client.Balance = -10;
    //await connection.Update(client);
}

//Delete a record
if (client is not null)
{
    //await connection.Delete(client);
    //await connection.Delete<Client>(13);
}

Console.ReadLine();

#else

var summary = BenchmarkDotNet.Running.BenchmarkRunner.Run(typeof(Program).Assembly);

#endif

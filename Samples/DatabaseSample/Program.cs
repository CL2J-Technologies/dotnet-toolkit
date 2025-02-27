using cl2j.Database;
using cl2j.Logging;
using DatabaseSample;
using DatabaseSample.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(builder => { builder.AddDebug().SetMinimumLevel(LogLevel.Trace); });
services.AddDatabase();
var serviceProvider = services.BuildServiceProvider();
serviceProvider.UseDatabase();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

var connectionString = "Data Source=.;Initial Catalog=DatabaseSample;Integrated Security=True;MultipleActiveResultSets=False;Connection Timeout=15;encrypt=true;trustServerCertificate=true";

// ---------------------------------------------------------------------------------------------

using var connection = new SqlConnection(connectionString);

await connection.DropTableIfExists<Product>();
await connection.DropTableIfExists<Category>();

//Create tables
if (!await connection.TableExists<Category>())
    await connection.CreateTable<Category>();

if (!await connection.TableExists<Product>())
    await connection.CreateTable<Product>();

//Insert data
var categories = DataGenerator.GenerateCategories();
foreach (var category in categories)
    await connection.Insert(category);

var products = DataGenerator.GenerateProducts(categories);
foreach (var product in products)
    await connection.Insert(product);


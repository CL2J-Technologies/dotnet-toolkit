using cl2j.Database;
using cl2j.FileStorage;
using cl2j.Logging;
using DatabaseSample.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = ConfigureServices();
var serviceProvider = services.BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

var connectionString = "Data Source=.;Initial Catalog=DatabaseSample;Integrated Security=True;MultipleActiveResultSets=False;Connection Timeout=15;encrypt=true;trustServerCertificate=true";
using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

await connection.CreateTable(typeof(Category));

static IServiceCollection ConfigureServices()
{
    var services = new ServiceCollection();

    // Build configuration
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false)
        .Build();
    services.AddSingleton(configuration);

    services.AddLogging(builder => { builder.AddDebug().SetMinimumLevel(LogLevel.Trace); });
    services.AddFileStorage();
    services.AddLogging(configuration);

    services.AddDatabase();

    return services;
}

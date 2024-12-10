using cl2j.SqlServerMaintenancePlan.IndexMaintenance;

var connectionString = "TO_CHANGE";
bool simulate = true;   //True to simulate, False to reindex indexes

var service = new SqlServerMaintenanceService(connectionString);
var result = await service.OptimizeIndexes(simulate);
foreach (var message in result.Messages)
    Console.WriteLine(message);
Console.WriteLine($"{result.RebuildCount} rebuilds, {result.ReorganizeCount} reorganizes");

Console.WriteLine($"Press any key to close the program.");
Console.ReadKey();
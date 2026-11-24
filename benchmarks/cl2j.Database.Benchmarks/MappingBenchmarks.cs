using System.Data;
using BenchmarkDotNet.Attributes;
using cl2j.Database.Benchmarks.Models;
using cl2j.Database.CommandBuilders;
using cl2j.Database.Descriptors;
using cl2j.Database.Helpers;
using Dapper;
using Microsoft.Data.SqlClient;
using static cl2j.Database.Benchmarks.BenchmarkSettings;

namespace cl2j.Database.Benchmarks;

/// <summary>
/// Maps rows from an in-memory DataTableReader: no server, no network, so what is left is the
/// mapper's own cost. EF Core cannot take part, since it builds readers itself rather than
/// mapping one it is handed.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 15)]
public class MappingBenchmarks : IDisposable
{
    private DataTable table = null!;

    private TableDescriptor descriptor = null!;

    public void Dispose()
    {
        table?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Params(1, 100, 10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        cl2j.Database.SqlServer.SqlServer.Register();

        // An unopened connection is enough to find the registered command builder.
        using var connection = new SqlConnection();
        var builder = CommandBuilderFactory.GetCommandBuilder(connection);
        descriptor = TableDescriptorFactory.Create(typeof(Client), builder.DatabaseFormatter);

        table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Balance", typeof(decimal));
        table.Columns.Add("Active", typeof(bool));
        table.Columns.Add("CreatedOn", typeof(DateTimeOffset));
        foreach (var c in GenerateClients(RowCount))
            table.Rows.Add(table.Rows.Count + 1, c.Name, c.Balance, c.Active, c.CreatedOn);

        // Fail now, with a clear message, if a contender does not map the table.
        using var reader = table.CreateDataReader();
        if ((await reader.Read<Client>(descriptor)).Count != RowCount)
            throw new InvalidOperationException("cl2j mapping returned the wrong row count");
    }

    [Benchmark(Baseline = true, Description = "Hand-written mapping")]
    public List<Client> HandWritten()
    {
        using var reader = table.CreateDataReader();
        var list = new List<Client>();
        while (reader.Read())
            list.Add(ReadClient(reader));
        return list;
    }

    [Benchmark(Description = "cl2j.Database Read<T>")]
    public async Task<List<Client>> Cl2j()
    {
        using var reader = table.CreateDataReader();
        return await reader.Read<Client>(descriptor);
    }

    [Benchmark(Description = "Dapper Parse<T>")]
    public List<Client> Dapper()
    {
        using var reader = table.CreateDataReader();
        return reader.Parse<Client>().ToList();
    }
}

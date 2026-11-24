using BenchmarkDotNet.Attributes;
using cl2j.Database.Benchmarks.Models;
using Dapper;
using Microsoft.EntityFrameworkCore;
using static cl2j.Database.Benchmarks.BenchmarkSettings;

namespace cl2j.Database.Benchmarks;

/// <summary>Select every row of the table.</summary>
public class ListBenchmarks : SqlBenchmarkBase
{
    [Params(1, 100, 10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        await PrepareTable(RowCount);
        Check((await Cl2j()).Count == RowCount && (await EfLinq()).Count == RowCount, "row count");
    }

    [GlobalCleanup]
    public Task Cleanup() => DropTable();

    [Benchmark(Baseline = true, Description = "ADO.NET (hand-written)")]
    public async Task<List<Client>> AdoNet()
    {
        await using var connection = await OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = SelectClients;
        await using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<Client>();
        while (await reader.ReadAsync())
            list.Add(ReadClient(reader));
        return list;
    }

    [Benchmark(Description = "cl2j.Database")]
    public async Task<List<Client>> Cl2j()
    {
        await using var connection = await OpenAsync();
        return await connection.Query<Client>(SelectClients);
    }

    [Benchmark(Description = "Dapper")]
    public async Task<List<Client>> Dapper()
    {
        await using var connection = await OpenAsync();
        return (await connection.QueryAsync<Client>(SelectClients)).AsList();
    }

    [Benchmark(Description = "EF Core 10 (LINQ, AsNoTracking)")]
    public async Task<List<Client>> EfLinq()
    {
        await using var context = new BenchContext(EfOptions);
        return await context.Clients.AsNoTracking().ToListAsync();
    }

    [Benchmark(Description = "EF Core 10 (raw SQL, AsNoTracking)")]
    public async Task<List<Client>> EfRaw()
    {
        await using var context = new BenchContext(EfOptions);
        return await context.Clients.FromSqlRaw(SelectClients).AsNoTracking().ToListAsync();
    }
}

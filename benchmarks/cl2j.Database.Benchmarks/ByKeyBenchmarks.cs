using BenchmarkDotNet.Attributes;
using cl2j.Database.Benchmarks.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static cl2j.Database.Benchmarks.BenchmarkSettings;

namespace cl2j.Database.Benchmarks;

/// <summary>Select one row by its key, with a parameter: the commonest call, and the one that exercises parameter handling.</summary>
public class ByKeyBenchmarks : SqlBenchmarkBase
{
    private const int Rows = 1_000;
    private const int Key = 500;

    [GlobalSetup]
    public async Task Setup()
    {
        await PrepareTable(Rows);
        Check((await Cl2jQueryKey())?.Id == Key && (await Cl2j())?.Id == Key && (await EfLinq())?.Id == Key, "key lookup");
    }

    [GlobalCleanup]
    public Task Cleanup() => DropTable();

    [Benchmark(Baseline = true, Description = "ADO.NET (hand-written)")]
    public async Task<Client?> AdoNet()
    {
        await using var connection = await OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = SelectClientById;
        cmd.Parameters.Add(new SqlParameter("@Id", Key));
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadClient(reader) : null;
    }

    [Benchmark(Description = "cl2j.Database (QuerySingle, SQL)")]
    public async Task<Client?> Cl2j()
    {
        await using var connection = await OpenAsync();
        return await connection.QuerySingle<Client>(SelectClientById, new { Id = Key });
    }

    [Benchmark(Description = "cl2j.Database (QueryKey)")]
    public async Task<Client?> Cl2jQueryKey()
    {
        await using var connection = await OpenAsync();
        return await connection.QueryKey<Client>(Key);
    }

    [Benchmark(Description = "Dapper")]
    public async Task<Client?> Dapper()
    {
        await using var connection = await OpenAsync();
        return await connection.QuerySingleOrDefaultAsync<Client>(SelectClientById, new { Id = Key });
    }

    [Benchmark(Description = "EF Core 10 (LINQ, AsNoTracking)")]
    public async Task<Client?> EfLinq()
    {
        await using var context = new BenchContext(EfOptions);
        return await context.Clients.AsNoTracking().SingleOrDefaultAsync(c => c.Id == Key);
    }

    [Benchmark(Description = "EF Core 10 (raw SQL, AsNoTracking)")]
    public async Task<Client?> EfRaw()
    {
        await using var context = new BenchContext(EfOptions);
        var rows = await context.Clients.FromSqlRaw(SelectClientById, new SqlParameter("@Id", Key)).AsNoTracking().ToListAsync();
        return rows.SingleOrDefault();
    }
}

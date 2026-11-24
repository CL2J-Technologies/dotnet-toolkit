using BenchmarkDotNet.Attributes;
using cl2j.Database.Benchmarks.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using static cl2j.Database.Benchmarks.BenchmarkSettings;

namespace cl2j.Database.Benchmarks;

/// <summary>
/// Insert one row and return the identity the server chose. The table grows during the run, which
/// does not matter for an insert into a table with no secondary index.
/// </summary>
public class InsertBenchmarks : SqlBenchmarkBase
{
    [GlobalSetup]
    public Task Setup() => PrepareTable(0);

    [GlobalCleanup]
    public Task Cleanup() => DropTable();

    [Benchmark(Baseline = true, Description = "ADO.NET (hand-written)")]
    public async Task<int> AdoNet()
    {
        var item = NewClient(1);
        await using var connection = await OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = InsertClient;
        cmd.Parameters.Add(new SqlParameter("@Name", item.Name));
        cmd.Parameters.Add(new SqlParameter("@Balance", item.Balance));
        cmd.Parameters.Add(new SqlParameter("@Active", item.Active));
        cmd.Parameters.Add(new SqlParameter("@CreatedOn", item.CreatedOn));
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    [Benchmark(Description = "cl2j.Database")]
    public async Task<int> Cl2j()
    {
        var item = NewClient(1);
        await using var connection = await OpenAsync();
        return await connection.Insert<Client, int>(item);
    }

    [Benchmark(Description = "Dapper")]
    public async Task<int> Dapper()
    {
        var item = NewClient(1);
        await using var connection = await OpenAsync();
        return await connection.ExecuteScalarAsync<int>(InsertClient, item);
    }

    [Benchmark(Description = "EF Core 10 (Add + SaveChanges)")]
    public async Task<int> Ef()
    {
        var item = NewClient(1);
        await using var context = new BenchContext(EfOptions);
        context.Clients.Add(item);
        await context.SaveChangesAsync();
        return item.Id;
    }
}

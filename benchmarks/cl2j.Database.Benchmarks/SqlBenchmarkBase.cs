using BenchmarkDotNet.Attributes;
using cl2j.Database.Benchmarks.Models;
using Microsoft.EntityFrameworkCore;

namespace cl2j.Database.Benchmarks;

/// <summary>
/// Shared setup of the end-to-end suites. Every contender opens a pooled connection per operation
/// and disposes it, so connection handling is the same for all of them. EF Core builds a context
/// per operation, as it is normally used; its options (and so its compiled model) are built once.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public abstract class SqlBenchmarkBase
{
    protected DbContextOptions<BenchContext> EfOptions { get; private set; } = null!;

    protected async Task PrepareTable(int rowCount)
    {
        cl2j.Database.SqlServer.SqlServer.Register();
        EfOptions = new DbContextOptionsBuilder<BenchContext>().UseSqlServer(BenchmarkSettings.GetConnectionString()).Options;

        // The table is dropped and rebuilt, so a run always starts from the same state.
        await using var connection = await BenchmarkSettings.OpenAsync();
        await connection.DropTableIfExists<Client>();
        await connection.CreateTableIfRequired<Client>();
        if (rowCount > 0)
            await connection.BulkInsert(BenchmarkSettings.GenerateClients(rowCount));
    }

    protected static async Task DropTable()
    {
        await using var connection = await BenchmarkSettings.OpenAsync();
        await connection.DropTableIfExists<Client>();
    }

    protected static void Check(bool ok, string what)
    {
        if (!ok)
            throw new InvalidOperationException($"Benchmark sanity check failed: {what}");
    }
}

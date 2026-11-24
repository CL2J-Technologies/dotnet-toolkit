using System.Data;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using cl2j.Database.Benchmarks.Models;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static cl2j.Database.Benchmarks.BenchmarkSettings;

namespace cl2j.Database.Benchmarks;

/// <summary>
/// Shared setup of the bulk suites. Each iteration starts from an empty table and fresh objects (EF
/// Core writes the identity back into the entities it inserts, so they cannot be reused), both built
/// in <c>IterationSetup</c>, outside the measurement. One invocation per iteration and a handful of
/// iterations: a single operation takes seconds, so the usual statistics would take hours.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 1, iterationCount: 5)]
public abstract class BulkBenchmarkBase
{
    protected List<Client> Items { get; private set; } = [];

    protected DbContextOptions<BenchContext> EfOptions { get; private set; } = null!;

    protected abstract int Count { get; }

    [GlobalSetup]
    public async Task Setup()
    {
        cl2j.Database.SqlServer.SqlServer.Register();
        EfOptions = new DbContextOptionsBuilder<BenchContext>().UseSqlServer(GetConnectionString()).Options;

        await using var connection = await OpenAsync();
        await connection.DropTableIfExists<Client>();
        await connection.CreateTableIfRequired<Client>();
    }

    [IterationSetup]
    public void EmptyTable()
    {
        using var connection = new SqlConnection(GetConnectionString());
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "TRUNCATE TABLE [Client]";
        cmd.ExecuteNonQuery();

        Items = GenerateClients(Count);
    }

    [IterationCleanup]
    public void CheckRowCount()
    {
        // Every contender must have written every row; a silent partial insert would look fast.
        using var connection = new SqlConnection(GetConnectionString());
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM [Client]";
        var rows = (int)cmd.ExecuteScalar()!;
        if (rows != Count)
            throw new InvalidOperationException($"Benchmark sanity check failed: {rows} rows written, {Count} expected");
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await using var connection = await OpenAsync();
        await connection.DropTableIfExists<Client>();
    }

    /// <summary>
    /// The hand-written floor: <c>SqlBulkCopy</c> fed from a <c>DataTable</c>, the way it is usually
    /// written by hand.
    /// </summary>
    protected async Task SqlBulkCopyHandWritten()
    {
        var table = new DataTable();
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Balance", typeof(decimal));
        table.Columns.Add("Active", typeof(bool));
        table.Columns.Add("CreatedOn", typeof(DateTimeOffset));
        foreach (var item in Items)
            table.Rows.Add(item.Name, item.Balance, item.Active, item.CreatedOn);

        await using var connection = await OpenAsync();
        using var bulkCopy = new SqlBulkCopy(connection) { DestinationTableName = "[Client]", BulkCopyTimeout = connection.ConnectionTimeout };
        foreach (DataColumn column in table.Columns)
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        await bulkCopy.WriteToServerAsync(table);
    }
}

/// <summary>
/// Insert many rows at once. Only cl2j.Database and hand-written ADO.NET use <c>SqlBulkCopy</c>; EF
/// Core batches multi-row <c>INSERT</c> statements; Dapper has no bulk API without a paid add-on (see
/// <see cref="DapperBulkInsertBenchmarks"/>).
/// </summary>
public class BulkInsertBenchmarks : BulkBenchmarkBase
{
    [Params(10_000, 100_000, 1_000_000)]
    public int RowCount { get; set; }

    protected override int Count => RowCount;

    [Benchmark(Baseline = true, Description = "ADO.NET SqlBulkCopy (hand-written)")]
    public Task AdoNet() => SqlBulkCopyHandWritten();

    [Benchmark(Description = "cl2j.Database BulkInsert")]
    public async Task Cl2j()
    {
        await using var connection = await OpenAsync();
        await connection.BulkInsert(Items);
    }

    [Benchmark(Description = "EF Core 10 (AddRange + SaveChanges)")]
    public async Task Ef()
    {
        await using var context = new BenchContext(EfOptions);
        context.Clients.AddRange(Items);
        await context.SaveChangesAsync();
    }
}

/// <summary>
/// Dapper's free way to insert a list: <c>ExecuteAsync</c> with an enumerable runs the statement once
/// per item. Measured at 10,000 rows only, in one transaction (its best case): at a million rows a
/// single operation takes minutes.
/// </summary>
public class DapperBulkInsertBenchmarks : BulkBenchmarkBase
{
    private const string Insert = "INSERT INTO [Client] ([Name], [Balance], [Active], [CreatedOn]) VALUES (@Name, @Balance, @Active, @CreatedOn)";

    [Params(10_000)]
    public int RowCount { get; set; }

    protected override int Count => RowCount;

    [Benchmark(Baseline = true, Description = "ADO.NET SqlBulkCopy (hand-written)")]
    public Task AdoNet() => SqlBulkCopyHandWritten();

    [Benchmark(Description = "Dapper (ExecuteAsync over the list, one transaction)")]
    public async Task Dapper()
    {
        await using var connection = await OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync(Insert, Items, transaction);
        await transaction.CommitAsync();
    }
}

using System.Data.Common;
using cl2j.Database.Benchmarks.Models;
using Microsoft.Data.SqlClient;

namespace cl2j.Database.Benchmarks;

internal static class BenchmarkSettings
{
    public const string ConnectionStringVariable = "CL2J_BENCH_SQL";

    public const string SelectClients = "SELECT [Id], [Name], [Balance], [Active], [CreatedOn] FROM [Client]";
    public const string SelectClientById = "SELECT [Id], [Name], [Balance], [Active], [CreatedOn] FROM [Client] WHERE [Id]=@Id";
    public const string InsertClient = "INSERT INTO [Client] ([Name], [Balance], [Active], [CreatedOn]) VALUES (@Name, @Balance, @Active, @CreatedOn); SELECT CAST(SCOPE_IDENTITY() AS int)";

    private static readonly DateTimeOffset Epoch = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The connection string of the SQL Server the benchmarks run against. The server only has to
    /// be reachable and empty: each suite creates its own table.
    /// </summary>
    public static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                $"The environment variable {ConnectionStringVariable} is not set. Set it to the connection string of an empty SQL Server " +
                "(for example a container), such as: Server=localhost,14330;User Id=sa;Password=<password>;TrustServerCertificate=True. " +
                "See benchmarks/README.md.");
        return value;
    }

    /// <summary>Deterministic rows: the same values for the same count on every run.</summary>
    public static List<Client> GenerateClients(int count)
    {
        var clients = new List<Client>(count);
        for (var i = 1; i <= count; ++i)
            clients.Add(NewClient(i));
        return clients;
    }

    public static Client NewClient(int i) => new()
    {
        Name = $"Client{i}",
        Balance = i,
        Active = i % 3 != 0,
        CreatedOn = Epoch.AddSeconds(i)
    };

    /// <summary>Hand-written mapping of the current row: the floor every mapper is measured against.</summary>
    public static Client ReadClient(DbDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        Balance = reader.GetDecimal(2),
        Active = reader.GetBoolean(3),
        CreatedOn = reader.GetFieldValue<DateTimeOffset>(4)
    };

    /// <summary>Connection policy of the end-to-end suites: a pooled connection per operation, for every contender.</summary>
    public static async Task<SqlConnection> OpenAsync()
    {
        var connection = new SqlConnection(GetConnectionString());
        await connection.OpenAsync();
        return connection;
    }
}

`cl2j.Database` is a fast, attribute-driven micro-ORM for .NET and SQL Server. You describe a table with a plain class; the library builds the SQL (create and drop table, insert, update, delete, queries by example or by key, bulk insert) and maps rows back to objects, with no `DbContext`, no change tracker and no mapping files. Reads run at the speed of hand-written ADO.NET and Dapper, and a million rows go in with one `BulkInsert` call in about a second and a half.

It is split in two packages:

- `cl2j.Database` - the abstraction: attributes, `IDatabase`, `DbConnection` extension methods, identifier generation.
- `cl2j.Database.SqlServer` - the SQL Server provider, built on `Microsoft.Data.SqlClient`. It is the only provider today.

Target framework: `net10.0`. It replaces `cl2j.DataStore.Database-deprecated` (see [Migrating](#migrating-from-cl2jdatastoredatabase-deprecated)).

# Why cl2j.Database

Numbers are from the [benchmarks](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/benchmarks/README.md): one run, on one workstation with SQL Server in a local container, steady state (the one-off cost of the first read of each type is not included). Read them as ratios, not as absolute times.

**Compared with Dapper**

- The same speed: reads are within noise of Dapper and of hand-written ADO.NET, at one row and at 10,000.
- It also writes the SQL. Dapper maps rows and leaves every statement to you; here `CreateTable`, `Insert`, `Update`, `DeleteKey`, `QueryKey` and `QueryKeys` come from the class.
- Bulk insert is built in. Dapper has no bulk API of its own (Dapper Plus is a commercial add-on), and a list passed to `ExecuteAsync` runs one `INSERT` per row: about 6 seconds for 10,000 rows.
- Parameters are declared from the column, not from the value, so the server's plan cache stays small, and a `varchar` key stays seekable ([details](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/parameters-and-plan-cache.md)).
- What you give up: SQL Server only for now, a Roslyn dependency at runtime, and one-off compile time on the first read of each type.

**Compared with EF Core 10**

- EF Core is 15 to 30 percent slower on the same reads. On small reads it allocates 6 to 14 times as much as hand-written code, where cl2j.Database allocates about 1.2 times.
- Bulk insert of 1,000,000 rows: 1.53 s, against 1.48 s for hand-written `SqlBulkCopy` and 34.4 s for EF Core `AddRange` + `SaveChanges`.
- What you give up: LINQ, joins, navigation properties, change tracking, migrations and concurrency tokens. You write SQL for anything beyond one table, and the schema is created from the class, never migrated.

# Features

Every page lists what the repository's tests prove and says plainly what the library does not do.

| Feature | Page |
|---|---|
| Install, wiring, a first CRUD, running without a host | [Getting started](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/getting-started.md) |
| Tables as classes: `[Table]`, `[Column]`, `[ForeignKey]`, `[Ignore]` | [Entities and attributes](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/entities-and-attributes.md) |
| Identity, `NEWID()`, string and generated keys, custom generators | [Keys](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/keys.md) |
| Queries by example, by key and by batch of keys, raw SQL, `Exists` | [Queries](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/queries.md) |
| Insert, update (whole row or one column), delete | [Insert, update and delete](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/insert-update-delete.md) |
| `SqlBulkCopy` from a list | [Bulk insert](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/bulk-insert.md) |
| Properties stored as JSON | [JSON columns](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/json-columns.md) |
| Commit and rollback across several writes | [Transactions](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/transactions.md) |
| Create, drop and check tables, foreign keys, defaults | [Schema](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/schema.md) |
| 18 tested types, what each keeps and loses | [Type mapping](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/type-mapping.md) |
| Parameter declarations and plan-cache behaviour | [Parameters and the plan cache](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/parameters-and-plan-cache.md) |
| Exceptions and logging | [Errors and diagnostics](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/errors-and-diagnostics.md) |
| The non-obvious behaviour, in one list | [Edge cases](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/edge-cases.md) |
| SQL Server specifics | [SQL Server provider](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/providers/sqlserver.md) |

# Getting started

1. Add the packages:

```powershell
Install-Package cl2j.Database
Install-Package cl2j.Database.SqlServer
```

2. Wire it up. There are four steps, and the last one is **outside dependency injection**:

```cs
using cl2j.Database;
using cl2j.Database.Databases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// 1. Options (DatabaseOptions).
builder.Services.AddDatabase();

// 2. The provider.
builder.Services.AddSingleton<IDatabase>(sp => new cl2j.Database.SqlServer.SqlServerDatabase(
    connectionString,
    new DatabaseOptions(),
    sp.GetRequiredService<ILogger<cl2j.Database.SqlServer.SqlServerDatabase>>()));

var host = builder.Build();

// 3. Logger and options, once the host is built.
host.UseDatabase();

// 4. The SQL Server command builder (static, process-wide registry).
cl2j.Database.SqlServer.SqlServer.Register();

host.Run();
```

**Without step 4, everything compiles, the application starts, and only the first database call fails:**

```
cl2j.Database.Exceptions.DatabaseException: No CommandBuilder registered for connection 'SqlConnection'
```

`CommandBuilderFactory` is a static registry, so the registration does not go through DI. Calling `Register` again replaces the previous registration of the same provider, which is how you change the identifier generator (see below).

Notes on the other steps:

- `UseDatabase` is defined on `IHost` and on `ServiceProvider`. It sets **static** state (`ConnectionExtensions.Logger` and `ConnectionExtensions.DatabaseOptions`), so call it once, at startup. It resolves a non-generic `ILogger` from the container; if none is registered the static logger stays `null` and the statement tracing done by the `DbConnection` extensions is off ([#68](https://github.com/CL2J-Technologies/dotnet-toolkit/issues/68)).
- The optional `register` parameter of `AddDatabase(databaseOptions, register)` is only a callback that receives the `IServiceCollection`. It does not register the provider.
- `DatabaseOptions` has `TraceLevel` (default `Trace`), `ExceptionLevel` (default `Debug`) and `BulkInsertTimeout`. `AddDatabase(o => ...)` configures the options read by `UseDatabase`; the instance given to `SqlServerDatabase` is the one used by `IDatabase` calls. Pass the same settings to both.

> `cl2j.Database` compiles the row-to-object mapper with Roslyn (through `cl2j.Scripting`) on the **first read** of each type. `Microsoft.CodeAnalysis.CSharp` must therefore be deployed with your application. See [#42](https://github.com/CL2J-Technologies/dotnet-toolkit/issues/42).

More: [Getting started](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/getting-started.md), including how to run without a host.

# Defining an entity

A table is a class with attributes from `cl2j.Database.DataAnnotations`. Every public readable property is a column unless it carries `[Ignore]`. The class must be `public`, and every mapped property needs a public setter.

```cs
using cl2j.Database.DataAnnotations;

[Table("Sample", Schema = "dbo")]
public class Sample
{
    [Column(Name = "Id", Key = KeyType.AutoGeneratedKey, Length = 36, KeyPrefix = "smp_")]
    public string Id { get; set; } = null!;

    [Column(Name = "Name", Length = 100, Required = true)]
    public string Name { get; set; } = null!;

    [Column(Name = "Note", Length = 500)]            // nullable: Required is false
    public string? Note { get; set; }

    [Column(Name = "Price", Length = 18, Decimals = 2)]
    public decimal Price { get; set; }

    [Column(Name = "CreatedAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column(Name = "Tags", Json = true)]             // serialized to a varchar(max) column
    public List<string> Tags { get; set; } = [];

    [Ignore]
    public string DisplayName => Name.ToUpperInvariant();
}
```

| Attribute | Properties |
|---|---|
| `[Table(name)]` | `Name`, `Schema`. Without the attribute the table name is the class name. |
| `[Column]` | `Name` (defaults to the property name), `TypeName` (raw provider type, overrides the mapping), `Length`, `Decimals`, `Key`, `Required`, `Json`, `Default`, `KeyPrefix` |
| `[ForeignKey(name, referenceTable, referenceField, referenceSchema)]` | Emitted as a constraint by `CreateTable`. `referenceSchema` is optional. |
| `[Ignore]` | The property is not a column. |

Details: [Entities and attributes](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/entities-and-attributes.md).

## Keys

`ColumnAttribute.Key` is a `KeyType`:

| Value | Meaning |
|---|---|
| `None` | Not a key. |
| `Key` | Primary key. `int`/`long` become `IDENTITY` and `Guid` becomes `DEFAULT NEWID()`; the server supplies them on insert. A `string` key is **not** generated: you set it yourself. |
| `AutoGeneratedKey` | `string` keys only. `Insert` and `BulkInsert` fill it in with the `IIdentifierGenerator` (the value is written to the property, so the entity you passed has its key after the call), and always overwrite a value you set. |
| `SelfGeneratedKey` | You generate the key yourself. `NewKey<T>()` returns a new value for the entity's `SelfGeneratedKey` column. |

If no column declares a key, a column named `Id` is treated as `Key`. `QueryKey` and `DeleteKey` need exactly one key column. `NewKey<T>()` only looks at `SelfGeneratedKey` columns, not at `AutoGeneratedKey` ones.

## KeyPrefix and column length

With the default generator, a generated key is `KeyPrefix + Guid.ToString("N")`: the prefix length **plus 32 characters**. That total has to fit `Length`.

```cs
[Column(Name = "Id", Key = KeyType.AutoGeneratedKey, Length = 36, KeyPrefix = "tac_")]
// -> tac_c0f79aeb73af4b1b8dad84842df7f5ff   (4 + 32 = 36)
```

A short prefix makes an identifier in a log attributable to its table. With no prefix the key is 32 characters, dashless; a 4-character prefix gives the same 36 characters as a dashed GUID, which helps when the table also holds keys created by other means. More: [Keys](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/keys.md).

## Type mapping (SQL Server)

`bool` -> `bit`, `byte` -> `tinyint`, `short` -> `smallint`, `int` -> `int`, `long` -> `bigint`, `float` -> `real`, `double` -> `float`, `decimal` -> `decimal(Length,Decimals)` (**`Length` is required**), `Guid` -> `uniqueidentifier`, `string` -> `varchar(Length)` or `varchar(max)`, `byte[]` -> `varbinary`, `DateTime` -> `datetime2`, `DateTimeOffset` -> `datetimeoffset`, `DateOnly` -> `date`, `TimeOnly`/`TimeSpan` -> `time`, `char` -> `char(1)`, enums -> `int` (not null), `Json = true` -> `varchar`. Nullable value types are unwrapped. Any other type throws `No column type for '...'. Declare one with [Column(TypeName = "...")].`

Three things to know:

- **`string`, `char` and JSON columns are `varchar`, not `nvarchar`.** A character outside the column collation is stored as `?` with no error. Use `TypeName = "nvarchar(100)"` when you need Unicode.
- **`Default` is appended to the DDL as `DEFAULT <value>`, but the library's own `Insert` never relies on it:** it sends every non-generated column, so a `null` property is written as `NULL`.
- **A key of type `Guid` is left to the server** (`DEFAULT NEWID()`): on `Insert`, a value set on the property is ignored.

What each type keeps and loses (`DateTime` loses its `Kind`, `TimeSpan` is limited to under a day, a `decimal` is rounded to `Decimals`, and so on) is in [Type mapping](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/type-mapping.md).

# Operations

`IDatabase` opens a connection per call and covers the common operations. For transactions, bulk insert and DDL, get a connection with `IDatabase.CreateConnection()` and use the extension methods in `ConnectionExtensions` (most have an overload taking a `CancellationToken` and an optional `DbTransaction`).

```cs
public class SampleRepository(IDatabase database)
{
    public Task<List<Sample>> All() => database.Query<Sample>();

    // WHERE built from the properties of the anonymous object (combined with AND, equality).
    public Task<List<Sample>> ByName(string name) => database.Query<Sample>(new { Name = name });

    public Task<Sample?> One(string name) => database.QuerySingle<Sample>(new { Name = name });

    public Task<List<Sample>> ByIds(IEnumerable<string> ids) => database.QueryKeys<Sample>(ids);

    // Raw SQL, parameters from an anonymous object.
    public Task<List<Sample>> Cheap(decimal max)
        => database.Query<Sample>("SELECT * FROM [Sample] WHERE [Price] < @max", new { max });

    public Task<bool> Exists(string name) => database.Exists<Sample>(new { Name = name });

    // Returns the entity, with the generated key filled in.
    public Task<Sample> Add(Sample sample) => database.Insert(sample);

    // WHERE on the key column(s).
    public Task<Sample> Save(Sample sample) => database.Update(sample);

    // Updates a single column (keys go into the WHERE clause).
    public Task Rename(Sample sample, string name) => database.Update(sample, "Name", name);

    public Task Remove(string id) => database.DeleteKey<Sample>(id);

    public Task<int> Purge() => database.Execute("DELETE FROM [Sample] WHERE [Price] = 0");
}
```

On a connection:

```cs
using var connection = await database.CreateConnection();

await connection.CreateTableIfRequired<Sample>();            // also: TableExists, CreateTable, DropTable, DropTableIfExists
await connection.BulkInsert(samples);                         // SqlBulkCopy; AutoGeneratedKey values are generated first
var one = await connection.QueryKey<Sample>("smp_...");       // by the single key column
var some = await connection.QueryKeys<Sample>(keys);          // IN (...) queries, batched; keys is IEnumerable<object>
await connection.Delete(sample);                              // by the key column(s) of the instance
```

Notes:

- A query by example with a `null` property matches nothing (it becomes `= NULL`); write `IS NULL` in SQL for that.
- `connection.Insert<TIn, TOut>(item)` returns the value of the `IDENTITY` column for `int`/`long` keys. `IDatabase.Insert` returns the entity and does not fill in an identity value.
- `BulkInsert` leaves out the same key columns as `Insert`: `int`, `long` and `Guid` `KeyType.Key` columns, which the server fills in. A `string` key is sent. Pass a list or an array, not a lazy sequence, when the entity has an `AutoGeneratedKey`. It uses `DatabaseOptions.BulkInsertTimeout` (from the static options), or the connection timeout when it is unset. It is not on `IDatabase`.
- `CreateTable` generates a `CREATE TABLE` from the attributes; it does not migrate an existing table, and it does not create the schema.
- `BuildSelectStatement<T>(sql)` (on both `IDatabase` and a connection) returns the `SELECT` the library would build for `T`, optionally followed by your own `WHERE`/`ORDER BY` text.

More: [Queries](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/queries.md), [Insert, update and delete](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/insert-update-delete.md), [Bulk insert](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/bulk-insert.md), [Transactions](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/transactions.md).

# Replacing the identifier generator

`IIdentifierGenerator` decides how `AutoGeneratedKey` values (and `NewKey<T>()`) are produced. The default is `GuidIdentifierGenerator`. Pass your own to `SqlServer.Register`:

```cs
using cl2j.Database.CommandBuilders;
using cl2j.Database.Descriptors;

public class PrefixedV7Generator : IIdentifierGenerator
{
    public string GenerateKey(ColumnDescriptor columnDescriptor)
        => (columnDescriptor.ColumnAtribute.KeyPrefix ?? "") + Guid.CreateVersion7().ToString("N");
}

cl2j.Database.SqlServer.SqlServer.Register(new PrefixedV7Generator());
```

Keep the generated length within the column `Length`. Registering again replaces the previous SQL Server registration (see [#29](https://github.com/CL2J-Technologies/dotnet-toolkit/issues/29)).

# Migrating from cl2j.DataStore.Database-deprecated

The deprecated package stopped at 5.2.1. The two packages coexist (the namespaces are `cl2j.DataStore.Database` and `cl2j.Database`), so you can migrate one repository at a time.

The first thing that breaks is the driver:

| | Driver | `Encrypt` default |
|---|---|---|
| `cl2j.DataStore.Database-deprecated` | `System.Data.SqlClient` | `false` |
| `cl2j.Database.SqlServer` | `Microsoft.Data.SqlClient` 6.x | `true` (since 4.0) |

A connection string that sets neither works with the old package and fails with the new one against a server using a self-signed certificate:

```
A connection was successfully established with the server, but then an error occurred during
the login process. (provider: SSL Provider, error: 0 - The certificate chain was issued by an
authority that is not trusted.)
```

Add `TrustServerCertificate=True` in development (a server with a valid certificate needs nothing), or set `Encrypt` explicitly.

# Testing

`cl2j.Database.Tests` tests statement generation without a server. `cl2j.Database.IntegrationTests` runs against a real SQL Server started with Testcontainers, so `dotnet test` needs Docker running; those tests fail rather than skip when it is not. See [CONTRIBUTING.md](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/CONTRIBUTING.md).

# Known limitations

- **ANSI key binding only applies to `QueryKey`.** `QuerySingle<T>(new { ... })` and the other anonymous-parameter paths do not get it, which can defeat the index on a `varchar` key column. See [#43](https://github.com/CL2J-Technologies/dotnet-toolkit/issues/43).
- **Roslyn is a runtime dependency**, needed on the first read, and nothing checks for it at startup. See [#42](https://github.com/CL2J-Technologies/dotnet-toolkit/issues/42).
- **One provider today.** SQL Server only; the schema is created from the class and never migrated; there are no joins, LINQ or change tracking. The full list of behaviours worth knowing is in [Edge cases](https://github.com/CL2J-Technologies/dotnet-toolkit/blob/main/cl2j.Database/docs/edge-cases.md).

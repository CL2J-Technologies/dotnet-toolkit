# Provider: SQL Server

Package `cl2j.Database.SqlServer`, built on `Microsoft.Data.SqlClient` 6.x. It is the only provider today.
Providers for MySQL and PostgreSQL are planned
([#80](https://github.com/CL2J-Technologies/dotnet-toolkit/issues/80),
[#81](https://github.com/CL2J-Technologies/dotnet-toolkit/issues/81)); this page is where each provider's
specifics live, and the other pages describe the behaviour that is common to them.

## Registration

```cs
cl2j.Database.SqlServer.SqlServer.Register();                          // default Guid generator
cl2j.Database.SqlServer.SqlServer.Register(new MyIdentifierGenerator()); // see Keys
```

`Register` is static and process-wide, and calling it again replaces the previous registration. The
connection you give the library has to be a `SqlConnection`
(`SqlConnection required. 'X' received.` otherwise). `SqlServerDatabase(connectionString, options, logger)`
opens a new connection per call.

## What is specific to SQL Server

| Area | Behaviour |
|---|---|
| Identifiers | Wrapped in `[...]`, not escaped. Never build them from user input. |
| Default schema | `dbo`. Other schemas must already exist. |
| Keys | `int`/`long` `Key` is `IDENTITY(1,1)`; `Guid` `Key` is `DEFAULT NEWID()`; the string key constraint is `CONSTRAINT [PK_<Table>] PRIMARY KEY CLUSTERED`. |
| Identity back | `Insert<TIn, TOut>` appends `SELECT CAST(SCOPE_IDENTITY() ...)`. |
| Strings | `varchar`; use `TypeName = "nvarchar(n)"` for Unicode. A `Json` column is `nvarchar(max)`. See [Type mapping](../type-mapping.md). |
| `DateTime` | `datetime2`; parameters are `datetime2` too. |
| Bulk insert | `SqlBulkCopy` with `SqlBulkCopyOptions.Default`. See [Bulk insert](../bulk-insert.md). |
| Table existence | `INFORMATION_SCHEMA.TABLES`. |
| Parameter declarations | `nvarchar(4000)`/`varchar(8000)` strings, `decimal(38,18)`, `datetime2`. See [Parameters and the plan cache](../parameters-and-plan-cache.md). |
| Connection strings | `Microsoft.Data.SqlClient` defaults `Encrypt=True`: a server with a self-signed certificate needs `TrustServerCertificate=True` in development. |

## Tested against

SQL Server 2022 in a Testcontainers container (`cl2j.Database.IntegrationTests`). Nothing is claimed for other
versions or for Azure SQL.

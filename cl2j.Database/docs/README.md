# cl2j.Database documentation

An attribute-driven micro-ORM for .NET, on SQL Server, MySQL and PostgreSQL. Start with [Getting started](getting-started.md).

| Page | What it covers |
|---|---|
| [Getting started](getting-started.md) | Install, the four registration steps, a first CRUD, running without a host. |
| [Entities and attributes](entities-and-attributes.md) | `[Table]`, `[Column]`, `[ForeignKey]`, `[Ignore]` and the rules a class must follow. |
| [Keys](keys.md) | Identity, `NEWID()`, string keys, generated keys, identifier generators. |
| [Queries](queries.md) | By example, by key, raw SQL, `Exists`, `BuildSelectStatement`. |
| [Insert, update and delete](insert-update-delete.md) | Writes, `UpdateColumn`, what `Update` sends. |
| [Bulk insert](bulk-insert.md) | `SqlBulkCopy`, key handling, timeout, numbers. |
| [JSON columns](json-columns.md) | `Json = true` and its pitfalls. |
| [Transactions](transactions.md) | `DbTransaction` on a connection. |
| [Schema](schema.md) | `CreateTable`, `DropTable`, foreign keys, defaults. |
| [Type mapping](type-mapping.md) | The 18 tested types and what each keeps or loses. |
| [Parameters and the plan cache](parameters-and-plan-cache.md) | How parameters are declared and why. |
| [Errors and diagnostics](errors-and-diagnostics.md) | Exceptions, logging, what reaches the logs. |
| [Edge cases](edge-cases.md) | The non-obvious behaviour, with its proof. |
| [SQL Server provider](providers/sqlserver.md) | SQL Server specifics. |
| [MySQL provider](providers/mysql.md) | MySQL 8.0.13 or later: registration, type mapping, bulk insert. |
| [PostgreSQL provider](providers/postgresql.md) | PostgreSQL 13 or later: registration, type mapping, binary COPY. |
| [Implementing a provider](providers/implementing-a-provider.md) | What a provider supplies, to add another database engine. |

Performance numbers are in [`benchmarks/README.md`](../../benchmarks/README.md).

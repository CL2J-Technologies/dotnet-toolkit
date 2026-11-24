# cl2j.Database.MySql

The MySQL provider for `cl2j.Database`, on MySqlConnector 2.6.2. It is a `RelationalCommandBuilder`
subclass (see [implementing-a-provider.md](implementing-a-provider.md)), so every statement, the
parameter binding and the reader are the ones SQL Server uses; this page covers what is MySQL's own.

**Requires MySQL 8.0.13 or later.** A Guid key is declared `DEFAULT (UUID())`, and an expression
default arrived in 8.0.13. The tests run against `mysql:8.4`.

## Registering

```csharp
cl2j.Database.MySql.MySql.Register();                 // once, at start-up
using var connection = new MySqlConnection("Server=...;Database=app;User ID=...;Password=...");
await connection.OpenAsync();
await connection.CreateTable<Customer>();
```

`MySql.Register(IIdentifierGenerator? = null)` mirrors `SqlServer.Register`. `MySqlDatabase`
(`new MySqlDatabase(connectionString, options, logger)`) is the `IDatabase` that opens a
`MySqlConnection` for you.

A type with no schema lives in the database of the connection (`DATABASE()`): MySQL calls a database
what SQL Server calls a schema. Names are quoted with backticks.

## Type mapping

| CLR type | MySQL column | Notes |
|---|---|---|
| enum | `int` | |
| `bool` | `tinyint(1)` | |
| `byte` | `tinyint unsigned` | a signed `tinyint` reads back as `sbyte` |
| `short` / `int` / `long` | `smallint` / `int` / `bigint` | |
| `float` | `double` | see below |
| `double` | `double` | |
| `decimal` | `decimal(p,s)` | precision required, as on SQL Server |
| `Guid` | `char(36)` | the form MySqlConnector reads back as a `Guid` |
| `char` | `char(1)` | |
| `string` | `varchar(n)`, or `longtext` with no length | no `varchar(max)` in MySQL |
| `byte[]` | `varbinary(n)`, or `longblob` with no length | |
| `DateTime` | `datetime(6)` | |
| `DateTimeOffset` | `datetime(6)` holding UTC | the offset is not kept |
| `DateOnly` | `date` | |
| `TimeOnly` / `TimeSpan` | `time(6)` | |
| JSON property | `json`, or `varchar(n)` with a length | |
| `int` / `long` key | `... AUTO_INCREMENT PRIMARY KEY` | key read with `SELECT LAST_INSERT_ID()` |
| `Guid` key | `char(36) DEFAULT (UUID()) PRIMARY KEY` | |

`float` is stored in a `double` column on purpose. MySQL's `FLOAT` travels as text with six
significant digits (16777216 reads back as 16777200) and refuses `float.MaxValue` as out of range;
a `double` holds every `float` exactly. The generated reader converts the value back to `float`.
Covered by `TypeMatrixTests` and `TypeEdgeValueTests` (the `float` cases).

## Bulk insert

`BulkInsert` has two strategies, chosen by the connection string, and both are all-or-nothing:

| Connection string | Strategy | Needs |
|---|---|---|
| contains `AllowLoadLocalInfile=true` | `MySqlBulkCopy` (`LOAD DATA LOCAL INFILE`) | the server must allow it too: `local_infile=ON`, off by default since 8.0 |
| otherwise | multi-row `INSERT` statements of 1000 rows | nothing |

Neither is atomic by itself: `LOAD DATA LOCAL` implies `IGNORE`, so a duplicate key is skipped and
the other rows are kept, and each `INSERT` statement commits on its own. When the caller passes no
transaction the provider opens one and rolls it back on failure, so a failed bulk insert stores
nothing, as `SqlBulkCopy` does. With a caller's transaction the rollback is the caller's.
The provider also throws a `DatabaseException` when `MySqlBulkCopy` reports a warning (a value
too long, NaN, an out-of-range number): the server would otherwise store a changed value and say
nothing. Covered by `MySqlBehaviourTests` (duplicate keys, empty input, value too long) and by
the shared bases, which run twice (`...BulkCopyTests` use the `AllowLoadLocalInfile` string).

Empty input writes nothing. Without `AllowLoadLocalInfile` the fallback is slower but needs no
server setting.

## Differences from SQL Server

Each line is pinned by a test in `MySqlBehaviourTests`, run against MySQL 8.4.

- **Offsets.** A `DateTimeOffset` is converted to UTC on all three write paths (parameter, batched
  insert, bulk copy); `08:00 -05:00` is stored as `13:00`, and read back with offset zero.
  `An_update_converts_the_offset_the_same_way`, `A_DateTimeOffset_is_stored_as_the_utc_instant...`.
- **Precision.** Six fractional digits; the seventh is truncated, never rounded (`.9999999` becomes
  `.999999`, it does not carry). `The_seventh_fractional_digit_is_truncated_not_rounded`.
- **`DateTimeKind` is ignored.** A `Local` value is written as the wall clock it carries, not moved to
  UTC, and reads back `Unspecified`. `The_kind_of_a_DateTime_is_ignored_not_converted`.
- **Range.** MySQL documents 1000-01-01 as the first supported `datetime`/`date`, but on 8.4 year 1
  and 9999-12-31 round-trip through all three paths, so the provider does not clamp. Stay at year
  1000 or later to be safe across servers. `Dates_below_the_documented_year_1000_minimum...`.
- **Collation.** The default `utf8mb4_0900_ai_ci` is accent- and case-insensitive: the keys
  `Renée` and `RENEE` collide (error 1062), and a lookup by key or by example finds a row whatever
  the case or accent. `String_keys_that_differ_only_by_accent_or_case_collide`.
- **Row size.** A row is limited to 65,535 bytes, and `varchar(n)` counts `n * 4` in utf8mb4: two
  `varchar(10000)` columns fail at `CreateTable` with error 1118, a single `varchar(20000)` with
  1074 (the limit is 16,383 characters). A string with no length is `longtext`, stored out of line,
  and has no such limit. `Two_varchar_columns_over_the_65535_byte_row_limit...`.
- **Value too long.** Under the default strict mode `Insert` and the batched insert throw
  `MySqlException` (1406, nothing stored). The bulk copy would truncate; the provider refuses it
  with a `DatabaseException`, so the three paths agree.
- **Duplicate key in a bulk insert.** Nothing is stored without a caller transaction. With one,
  the batched strategy keeps the statements before the failing one (2000 of 2500 rows in the test)
  and the bulk copy keeps every row but the duplicate (2499), until the caller rolls back.
- **`float`** is a `double` column (above); **`TimeSpan`** is a `time(6)` and holds up to
  +-838:59:59, so a span over a day is stored, not refused.

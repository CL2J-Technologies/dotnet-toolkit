# Benchmarks

`cl2j.Database.Benchmarks` compares cl2j.Database with Dapper and EF Core against SQL Server,
using [BenchmarkDotNet](https://benchmarkdotnet.org). It is not a test project and CI does not run
it; CI only builds it.

## What is measured

Every suite has a hand-written ADO.NET baseline (typed `GetInt32`/`GetString`/... calls, no
reflection). The `Ratio` column is relative to it, so 1.00 means "as fast as hand-written code".
All benchmarks are async, return what they read (so nothing is optimised away), and run with
`[MemoryDiagnoser]`: allocations matter as much as time for a mapper.

| Suite | Shape | Contenders |
| --- | --- | --- |
| `ListBenchmarks` | `SELECT` every row, 1 / 100 / 10,000 rows | cl2j `Query`, Dapper `QueryAsync`, EF Core LINQ and raw SQL (both `AsNoTracking`) |
| `ByKeyBenchmarks` | one row by key, parameterized (1,000-row table) | cl2j `QuerySingle` (SQL) and `QueryKey`, Dapper `QuerySingleOrDefaultAsync`, EF Core LINQ and raw SQL |
| `InsertBenchmarks` | one insert, identity returned | cl2j `Insert<T, int>`, Dapper `ExecuteScalarAsync`, EF Core `Add` + `SaveChangesAsync` |
| `MappingBenchmarks` | map 1 / 100 / 10,000 rows from an in-memory `DataTableReader`, **no database** | cl2j `Read<T>`, Dapper `Parse<T>` |
| `BulkInsertBenchmarks` | insert 10,000 / 100,000 / 1,000,000 rows into an empty table | cl2j `BulkInsert`, hand-written `SqlBulkCopy` (baseline), EF Core `AddRange` + `SaveChangesAsync` |
| `DapperBulkInsertBenchmarks` | insert 10,000 rows | Dapper `ExecuteAsync` over the list in one transaction, hand-written `SqlBulkCopy` |

The mapping suite isolates the library's own overhead, which the network round trip hides in the
three end-to-end suites. EF Core cannot take part in it: it builds its own readers and has no
"map this reader" entry point.

Each end-to-end suite drops and recreates its `Client` table in `[GlobalSetup]` (using
cl2j.Database DDL and `BulkInsert`) and seeds deterministic rows, so all it needs is an empty
server. The table is dropped again in `[GlobalCleanup]`.

### Connection policy

Every contender in the end-to-end suites opens a pooled `SqlConnection` per operation and disposes
it, which is how application code normally behaves. EF Core builds a `DbContext` per operation too,
from options (and so a compiled model) created once. Nobody reuses a connection another contender
could not.

### What this does not measure

- **Cold start.** cl2j.Database compiles a reader per type on first use; BenchmarkDotNet measures
  steady state after warm-up, so that one-off cost is excluded.
- **Large result sets, wide rows, joins, JSON columns, transactions, concurrency.**
- **EF Core features** such as change tracking, relationships and migrations. The EF numbers are
  its read-only, no-tracking path, which is the fair comparison with a mapper.
- **Anything about a production server.** See the caveat below.

## Run it

You need the .NET 10 SDK and a SQL Server. Any empty one will do; a container is the easiest:

```bash
docker run -d --name cl2j-bench -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=<a strong password>" \
  -p 14330:1433 mcr.microsoft.com/mssql/server:2022-latest

export CL2J_BENCH_SQL="Server=localhost,14330;User Id=sa;Password=<the same password>;TrustServerCertificate=True"
dotnet run -c Release --project benchmarks/cl2j.Database.Benchmarks

docker rm -f cl2j-bench
```

`CL2J_BENCH_SQL` is required; the program stops with a message if it is missing (except when only
the mapping suite is selected, which needs no server). Pass BenchmarkDotNet arguments after `--`
to select suites, for example `-- --filter "*Mapping*"` or `-- --filter "*ByKey*"`.

The whole run takes about 6 minutes. The three end-to-end suites use BenchmarkDotNet's
`ShortRun` job (1 launch, 3 warm-ups, 3 iterations) to stay in that budget; the mapping suite,
which is quiet, uses 3 warm-ups and 15 iterations. Treat the `Error` column of the end-to-end
results as real: with three iterations, differences of a few percent are noise.

## Results

BenchmarkDotNet 0.15.8, .NET 10.0.15, Microsoft.Data.SqlClient 6.1.8, EF Core 10.0.15, Dapper 2.1.100,
SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-latest`) in Docker, Release build.

**Caveat.** One run, on a single AMD Ryzen 9 9950X workstation (32 logical cores), inside WSL 2
(Ubuntu), with SQL Server in a Docker container on the same machine, and other work running on it. Loopback
networking is far faster than a real network, so the database round trip is smaller here than in
any deployment. Read the numbers as relative (ratio and allocations), not absolute, and re-run on
your own setup before drawing a conclusion.

### List: select all rows

| Method | Rows | Mean | Ratio | Allocated | Alloc ratio |
| --- | ---: | ---: | ---: | ---: | ---: |
| ADO.NET (hand-written) | 1 | 697 us | 1.00 | 5.75 KB | 1.00 |
| cl2j.Database | 1 | 688 us | 0.99 | 6.88 KB | 1.20 |
| Dapper | 1 | 681 us | 0.98 | 6.81 KB | 1.18 |
| EF Core 10 (LINQ) | 1 | 839 us | 1.20 | 73.84 KB | 12.84 |
| EF Core 10 (raw SQL) | 1 | 874 us | 1.25 | 80.31 KB | 13.97 |
| ADO.NET (hand-written) | 100 | 734 us | 1.00 | 17.95 KB | 1.00 |
| cl2j.Database | 100 | 790 us | 1.08 | 22.08 KB | 1.23 |
| Dapper | 100 | 747 us | 1.02 | 27.43 KB | 1.53 |
| EF Core 10 (LINQ) | 100 | 907 us | 1.24 | 104.61 KB | 5.83 |
| EF Core 10 (raw SQL) | 100 | 861 us | 1.17 | 111.08 KB | 6.19 |
| ADO.NET (hand-written) | 10,000 | 5.40 ms | 1.00 | 1,733 KB | 1.00 |
| cl2j.Database | 10,000 | 5.03 ms | 0.93 | 1,664 KB | 0.96 |
| Dapper | 10,000 | 5.65 ms | 1.05 | 2,228 KB | 1.29 |
| EF Core 10 (LINQ) | 10,000 | 6.69 ms | 1.24 | 3,693 KB | 2.13 |
| EF Core 10 (raw SQL) | 10,000 | 7.06 ms | 1.31 | 3,700 KB | 2.14 |

### By key: one row, parameterized

| Method | Mean | Ratio | Allocated | Alloc ratio |
| --- | ---: | ---: | ---: | ---: |
| ADO.NET (hand-written) | 671 us | 1.00 | 6.57 KB | 1.00 |
| cl2j.Database (QuerySingle, SQL) | 759 us | 1.13 | 7.86 KB | 1.20 |
| cl2j.Database (QueryKey) | 711 us | 1.06 | 8.67 KB | 1.32 |
| Dapper | 680 us | 1.01 | 7.85 KB | 1.20 |
| EF Core 10 (LINQ) | 860 us | 1.28 | 76.21 KB | 11.60 |
| EF Core 10 (raw SQL) | 839 us | 1.25 | 81.96 KB | 12.47 |

### Insert: one row

| Method | Mean | Ratio | Allocated | Alloc ratio |
| --- | ---: | ---: | ---: | ---: |
| ADO.NET (hand-written) | 1.85 ms | 1.00 | 7.42 KB | 1.00 |
| cl2j.Database | 1.96 ms | 1.06 | 9.55 KB | 1.29 |
| Dapper | 1.82 ms | 0.98 | 8.21 KB | 1.11 |
| EF Core 10 (Add + SaveChanges) | 2.04 ms | 1.11 | 83.73 KB | 11.28 |

### Mapping only (no database)

| Method | Rows | Mean | Ratio | Allocated | Alloc ratio |
| --- | ---: | ---: | ---: | ---: | ---: |
| Hand-written mapping | 1 | 234 ns | 1.00 | 1.05 KB | 1.00 |
| cl2j.Database `Read<T>` | 1 | 512 ns | 2.19 | 1.58 KB | 1.51 |
| Dapper `Parse<T>` | 1 | 380 ns | 1.62 | 1.12 KB | 1.07 |
| Hand-written mapping | 100 | 7.48 us | 1.00 | 19.34 KB | 1.00 |
| cl2j.Database `Read<T>` | 100 | 7.57 us | 1.01 | 19.88 KB | 1.03 |
| Dapper `Parse<T>` | 100 | 7.23 us | 0.97 | 19.41 KB | 1.00 |
| Hand-written mapping | 10,000 | 2.02 ms | 1.00 | 1,898 KB | 1.00 |
| cl2j.Database `Read<T>` | 10,000 | 1.93 ms | 0.96 | 1,898 KB | 1.00 |
| Dapper `Parse<T>` | 10,000 | 1.91 ms | 0.95 | 1,898 KB | 1.00 |

### Reading the results

- Against a real server, cl2j.Database and Dapper are within noise of each other and of the
  hand-written loop for lists; the round trip dominates. The by-key and insert rows show a fixed
  cost of roughly 1.2 to 1.3 times the hand-written allocations for both mappers.
- EF Core allocates 6 to 14 times as much on small reads, because a context, a query pipeline and
  result materialisation are built per call. Its time overhead is about 20 to 30 percent here.
- Without the network, the mapper itself is at parity with Dapper and hand-written code from about
  100 rows. Only at a single row does cl2j.Database's fixed per-call cost show (about 280 ns more than
  hand-written code), which a real round trip of hundreds of microseconds makes invisible.
- The ShortRun `Error` values are wide (tens of percent in places). Differences of a few percent
  between cl2j.Database, Dapper and ADO.NET in the end-to-end tables are not significant.

## Comparing cl2j.Database versions

The same benchmark code can run against a published package instead of this repository's source:

```bash
CL2J_DB_VERSION=4.1.1 CL2J_BENCH_JOB=default dotnet run -c Release --project benchmarks/cl2j.Database.Benchmarks -- --filter "*Mapping*"
```

`CL2J_DB_VERSION` swaps the project references for `cl2j.Database` and `cl2j.Database.SqlServer` at that
version (4.1.1 and 5.0.0 compile against the benchmarks unchanged). `CL2J_BENCH_JOB=default` switches
the end-to-end suites from `ShortRun` to BenchmarkDotNet's default job, which gives tight error bars.

### Was there a read regression? (one run, same machine as above)

No. cl2j.Database 4.1.1 (before the `SequentialAccess` removal and the `GetOrdinal` change), 5.0.0 and
6.5.2 are within noise of each other on time; the end-to-end calls sit at hand-written ADO.NET speed
in all three. The only visible change is allocations: 4.1.1 allocated more per call
(List 1 row 10.05 KB vs 6.83 KB, ByKey 11.7 KB vs 7.8 KB), because `SequentialAccess` was costing
memory, not saving it.

| Mean (default job) | 4.1.1 | 5.0.0 | 6.5.2 |
| --- | ---: | ---: | ---: |
| Mapping 1 row | 472 ns | 483 ns | 493 ns |
| Mapping 100 rows | 6.97 us | 6.98 us | 7.09 us |
| Mapping 10,000 rows | 1.85 ms | 1.85 ms | 1.88 ms |
| List 100 rows | 757 us | 723 us | 699 us |
| List 10,000 rows | 5.00 ms | 4.80 ms | 4.97 ms |
| ByKey (QuerySingle) | 735 us | 709 us | 680 us |

The 1-row mapping case is about 2.3 times hand-written code (Dapper: 1.5 times) in every version: a
fixed per-call cost, not a regression.

### Where the fixed per-call cost goes (throwaway experiments on 6.5.2)

| Experiment | Mapping 1 row | Mapping 100 / 10k | End-to-end |
| --- | ---: | ---: | ---: |
| baseline 6.5.2 | 493 ns, 1.58 KB | 7.09 us / 1.88 ms | |
| `Script.Execute` through a cached delegate instead of `InvokeMember` | 284 ns (-42%), 1.21 KB (-0.37 KB) | no change | ByKey -0.37 KB |
| fixed ordinals instead of `GetOrdinal` | 461 ns (-6%, within noise), same bytes | no change | ByKey -0.4 KB |
| `SequentialAccess` back in `ReadBehavior` | not applicable | | List/ByKey: no time or allocation change |

Only the delegate was kept. Going back on the ordinals would bring back #25 for a gain within noise,
and `SequentialAccess` changed nothing measurable while making the reader depend on column order.

### 6.5.3: the bound delegate (default job, same machine)

`Script.Execute<TIn, TOut>` now binds the generated method to a `Func<TIn, TOut>` once and calls that,
instead of going through `Type.InvokeMember` on every call.

| Method | Rows | Mean | Ratio | Allocated | Alloc ratio |
| --- | ---: | ---: | ---: | ---: | ---: |
| Hand-written mapping | 1 | 218 ns | 1.00 | 1.05 KB | 1.00 |
| cl2j.Database `Read<T>` | 1 | 284 ns | 1.30 | 1.21 KB | 1.16 |
| Dapper `Parse<T>` | 1 | 338 ns | 1.55 | 1.12 KB | 1.07 |
| Hand-written mapping | 100 | 6.74 us | 1.00 | 19.34 KB | 1.00 |
| cl2j.Database `Read<T>` | 100 | 6.68 us | 0.99 | 19.51 KB | 1.01 |
| Dapper `Parse<T>` | 100 | 6.63 us | 0.98 | 19.41 KB | 1.00 |
| Hand-written mapping | 10,000 | 1.88 ms | 1.00 | 1,898 KB | 1.00 |
| cl2j.Database `Read<T>` | 10,000 | 2.07 ms | 1.11 | 1,898 KB | 1.00 |
| Dapper `Parse<T>` | 10,000 | 2.01 ms | 1.07 | 1,898 KB | 1.00 |

The fixed per-call cost drops from about 2.3 times hand-written code to 1.3 times, below Dapper's.
The 10,000-row rows are noisy in this run (Dapper's error is ±160 us); the earlier runs had all three
at 1.85 to 1.93 ms, and the delegate does not touch the per-row loop. End to end, the gain is
invisible behind the round trip.

### 6.5.2 against 6.5.3, end to end (default job, one session)

Both versions were measured in the same session, against the same SQL Server container: 6.5.3 is
this repository's source, and 6.5.2 is the published package (`CL2J_DB_VERSION=6.5.2`, cl2j cases only).
Each cell gives the mean time, then the allocations.

| Case | ADO.NET | cl2j 6.5.2 | cl2j 6.5.3 | Dapper | EF Core (LINQ) | EF Core (raw SQL) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| List, 1 row | 682 us, 5.70 KB | 696 us, 6.83 KB | 665 us, 6.46 KB | 673 us, 6.66 KB | 801 us, 73.8 KB | 776 us, 80.3 KB |
| List, 100 rows | 712 us, 17.9 KB | 731 us, 22.0 KB | 710 us, 21.7 KB | 666 us, 27.3 KB | 813 us, 104.6 KB | 828 us, 111.1 KB |
| List, 10,000 rows | 4.79 ms, 1,731 KB | 4.91 ms, 1,664 KB | 4.85 ms, 1,664 KB | 5.45 ms, 2,228 KB | 6.09 ms, 3,693 KB | 6.36 ms, 3,700 KB |
| By key (`QuerySingle`) | 655 us, 6.52 KB | 696 us, 7.82 KB | 671 us, 7.45 KB | 716 us, 7.70 KB | 809 us, 76.2 KB | 773 us, 82.0 KB |
| By key (`QueryKey`) | | 708 us, 8.63 KB | 651 us, 8.26 KB | | | |
| Insert, one row | 1.69 ms, 7.38 KB | 1.81 ms, 9.5 KB | 1.70 ms, 9.5 KB | 1.71 ms, 8.05 KB | 1.85 ms, 83.6 KB | |

- The measurable difference between the two versions is in allocations: 0.37 KB less per read in
  6.5.3. Allocations are deterministic, so this holds from run to run.
- The time differences between 6.5.2 and 6.5.3 (3 to 7 percent) are drift between the two passes,
  not the change: the delegate saves about 0.2 us per call. Insert proves it. It does not go through
  the changed code (9.5 KB in both versions), yet it shows the same 6 percent gap.
- The overall picture is unchanged. cl2j.Database is at parity with hand-written ADO.NET and Dapper,
  and at 10,000 rows it is faster and allocates less than Dapper. EF Core is 15 to 30 percent slower
  and allocates 6 to 14 times as much on small reads.

## Bulk insert

`BulkInsertBenchmarks` and `DapperBulkInsertBenchmarks` insert many rows into an empty `Client` table.
Each iteration starts from a truncated table and freshly built objects (outside the measurement) and
checks the row count afterwards, so a partial insert cannot look fast. One operation takes up to
tens of seconds, so these suites use BenchmarkDotNet's `Monitoring` strategy: one invocation per
iteration, one warm-up and five iterations. The error bars at 10,000 and 100,000 rows are wide; at a
million rows they are within 4 percent.

Run them with a connection timeout long enough for a million rows (`SqlBulkCopy` and
`BulkInsert` both use it):

```bash
CL2J_BENCH_SQL="...;Connection Timeout=300" dotnet run -c Release --project benchmarks/cl2j.Database.Benchmarks -- --filter "*BulkInsert*"
```

### Results (same machine, 6.5.3)

| Method | Rows | Mean | Ratio | Allocated | Alloc ratio |
| --- | ---: | ---: | ---: | ---: | ---: |
| ADO.NET `SqlBulkCopy` (hand-written) | 10,000 | 25.0 ms | 1.00 | 7.07 MB | 1.00 |
| cl2j.Database `BulkInsert` | 10,000 | 25.6 ms | 1.02 | 8.06 MB | 1.14 |
| EF Core 10 (`AddRange` + `SaveChanges`) | 10,000 | 448 ms | 17.9 | 74.0 MB | 10.5 |
| Dapper (`ExecuteAsync` over the list, one transaction) | 10,000 | 6.21 s | 270 | 32.5 MB | 4.6 |
| ADO.NET `SqlBulkCopy` (hand-written) | 100,000 | 183 ms | 1.00 | 67.4 MB | 1.00 |
| cl2j.Database `BulkInsert` | 100,000 | 207 ms | 1.13 | 77.3 MB | 1.15 |
| EF Core 10 (`AddRange` + `SaveChanges`) | 100,000 | 3.39 s | 18.5 | 734 MB | 10.9 |
| ADO.NET `SqlBulkCopy` (hand-written) | 1,000,000 | 1.48 s | 1.00 | 649 MB | 1.00 |
| cl2j.Database `BulkInsert` | 1,000,000 | **1.53 s** | 1.04 | 748 MB | 1.15 |
| EF Core 10 (`AddRange` + `SaveChanges`) | 1,000,000 | 34.4 s | 23.2 | 7,285 MB | 11.2 |

The Dapper row comes from its own suite, where the `SqlBulkCopy` baseline measured 23.0 ms; its ratio
is against that run.

- **A million rows in about 1.5 seconds**, within 4 percent of hand-written `SqlBulkCopy`, from one
  call on a plain list. The 15 percent extra allocation is the `DataTable` `BulkInsert` builds from
  the attributes; the hand-written baseline builds the same table by hand.
- **EF Core** batches multi-row `INSERT` statements and tracks every entity: 18 to 23 times slower,
  and 7 GB allocated for a million rows. Its bulk extensions are third-party packages, and some of
  them are commercial.
- **Dapper** has no bulk API of its own (Dapper Plus is a commercial add-on). Passing a list to
  `ExecuteAsync` runs one `INSERT` per item: 6 seconds for 10,000 rows even in one transaction, so a
  million rows would take about 10 minutes. It was not measured at that size.

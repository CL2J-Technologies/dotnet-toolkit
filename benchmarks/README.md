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

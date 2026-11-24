# Type mapping (SQL Server)

Eighteen types are exercised end to end, each as a plain, a `Required` and a nullable property, through
`Insert`, `BulkInsert`, `Query` by example and a `NULL` round trip (`TypeMatrixTests`, `TypeMappingTests`,
`TypeEdgeValueTests`, `WideValueTests`).

| C# type | SQL Server column | Keeps | Does not keep |
|---|---|---|---|
| `bool` | `bit` | | |
| `byte` | `tinyint` | 0 to 255 | |
| `short`, `int`, `long` | `smallint`, `int`, `bigint` | the full range of each | |
| `float` | `real` | its own digits | `NaN` and infinity are refused (SQL Server has neither) |
| `double` | `float` | every digit | `NaN` and infinity are refused |
| `decimal` | `decimal(Length, Decimals)` | up to 38 digits, `Length` is **required** | more decimals than `Decimals` are **rounded** by the server; more digits than `Length` are refused |
| `Guid` | `uniqueidentifier` | | |
| `string` | `varchar(Length)` / `varchar(max)` | an empty string and `null` stay apart | **characters outside the collation** (see below) |
| `byte[]` | `varbinary(Length)` / `varbinary(max)` | an empty array and `null` stay apart | |
| `DateTime` | `datetime2` | ticks, to the 100 ns of `datetime2(7)` | **`Kind`**: reads back `Unspecified` |
| `DateTimeOffset` | `datetimeoffset` | instant and offset, extreme offsets included | |
| `DateOnly` | `date` | | |
| `TimeOnly` | `time` | | |
| `TimeSpan` | `time` | up to just under 24 hours | a day or more, or a negative value, is **refused** (it throws; it is not truncated) |
| `char` | `char(1)` | | a character outside the collation (see below) |
| enum | `int`, `NOT NULL` | the numeric value, including a value that is not defined in the enum | |
| `Json = true` | `nvarchar(max)` (`nvarchar(Length)`) | see [JSON columns](json-columns.md); Unicode intact | |

Nullable value types (`int?`, `DateTime?`) are unwrapped: the column type is that of the underlying type, and
the column allows `NULL`. A non-nullable enum property is `NOT NULL`; `Kind?` gives a nullable `int`
column.

## Strings are `varchar`, not `nvarchar`

`string` and `char` columns are non-Unicode. A character outside the column collation (Windows-1252 by default)
is stored as `?` or a look-alike, **with no error**. Declare a Unicode column when you need it:

```cs
[Column(Name = "Name", TypeName = "nvarchar(100)")]
public string Name { get; set; } = null!;

[Column(Name = "Initial", TypeName = "nchar(1)")]
public char Initial { get; set; }
```

Parameters follow the column: with `TypeName = "nvarchar(...)"` the value is sent as `nvarchar`, so nothing
is lost on the way in. See [Parameters and the plan cache](parameters-and-plan-cache.md).

## `TypeName` overrides everything

`[Column(TypeName = "...")]` is used as the SQL type verbatim (`"money"`, `"nvarchar(max)"`,
`"datetime"`). The library does not parse it, apart from the string and date rules in
[Parameters and the plan cache](parameters-and-plan-cache.md). Any C# type with no mapping must use it:

```
No column type for 'X'. Declare one with [Column(TypeName = "...")].
```

## Reading tables the library did not create

The generated reader uses typed getters, so **the column type has to be the property's own type**. Only the
combinations below are covered by tests (`TypeKeyAndForeignTableTests`):

| Column | Property | |
|---|---|---|
| `nvarchar` | `string` | reads, Unicode intact |
| `datetime`, `smalldatetime`, `datetime2` | `DateTime` | reads |
| `money`, `numeric` | `decimal` | reads |
| `bit` | `bool` / `bool?` | reads; a `NULL` bit gives `null` |
| `int` | enum | reads |
| `tinyint` | `int` or an enum | **throws `InvalidCastException`** |
| a narrower integer | a wider integer | **does not widen**: throws |

Choose the property type to match the column (`byte` for `tinyint`, `short` for `smallint`), or `CAST` in
the `SELECT`.

## Null handling on read

- A `Required` property reads a `NULL` as an error (`SqlNullValueException`).
- An optional property maps `NULL` to the type default: `0`, `false`, `null` (`int?` and reference types keep
  `null`).
- `Required = false` on an `int` therefore cannot tell `NULL` from `0` when you read it back; use `int?`.

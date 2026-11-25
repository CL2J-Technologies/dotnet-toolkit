# Queries

All queries return `List<T>`, `T?` or `bool`, and map rows with the generated reader (see
[Entities and attributes](entities-and-attributes.md)). `IDatabase` opens a connection per call; the same
methods exist on a `DbConnection`.

## By example

```cs
var all = await database.Query<Customer>();                             // every row
var ada = await database.Query<Customer>(new { Name = "Ada" });         // WHERE [Name] = @Name
var both = await database.Query<Customer>(new { Name = "Ada", Email = "ada@example.com" }); // AND
var one = await database.QuerySingle<Customer>(new { Id = id });        // first row, or null
bool there = await database.Exists<Customer>(new { Id = id });
```

- The properties of the anonymous object become equality tests, combined with `AND`. Values are
  parameters. A `string` is sent as `nvarchar`, even against a `varchar` key: see
  [Anonymous parameters and varchar keys](parameters-and-plan-cache.md#anonymous-parameters-and-varchar-keys).
- The property names must match **column names** (the `[Column(Name = ...)]` name when there is one).
- **There is no `LIKE`, range, `OR` or `IN` in a query by example.** Use SQL for those.
- **A `null` property matches nothing.** `new { Name = (string?)null }` becomes `= NULL`, which is never
  true. Write `WHERE [Name] IS NULL` yourself. (A `null` example object, `Query<T>((object?)null)`, returns every
  row; a bare `null` literal binds to the SQL overload and throws.)
- `Exists` returns `true` when at least one row matches.

## By key

```cs
await using var connection = await database.CreateConnection();
var one = await connection.QueryKey<Customer>(id);          // connection only; null when absent
var some = await connection.QueryKeys<Customer>(ids.Cast<object>());
var some2 = await database.QueryKeys<Customer>(ids);        // IDatabase: IEnumerable<string>
```

See [Keys](keys.md#reading-and-deleting-by-key) for the single-key requirement and the batching.

## Raw SQL

```cs
var cheap = await database.Query<Product>(
    "SELECT * FROM [Product] WHERE [Price] < @max ORDER BY [Name]", new { max = 20m });

var one = await database.QuerySingle<Product>("SELECT TOP 1 * FROM [Product] WHERE [Name] = @n", new { n = "x" });
```

- Parameters come from an anonymous object (or any object whose properties are the parameter names).
- Column names in the result are matched to properties **by name**; extra columns are ignored, and a column
  the class needs but the `SELECT` leaves out throws an error that names it.
- The statement may select from any table, view or expression, so a class can be a read model for a
  query that has nothing to do with `[Table]`.

### Let the library write the column list

`BuildSelectStatement<T>(sql)` returns the `SELECT` the library would build for `T`, followed by your own text:

```cs
var sql = await database.BuildSelectStatement<Customer>("WHERE [CreatedAt] > @since ORDER BY [CreatedAt] DESC");
var recent = await database.Query<Customer>(sql, new { since = DateTimeOffset.UtcNow.AddDays(-7) });
```

On a connection it is synchronous: `connection.BuildSelectStatement<Customer>("WHERE ...")`.

## `Execute`

`Execute(sql, param)` runs any statement and returns the number of rows affected:

```cs
int deleted = await database.Execute("DELETE FROM [Customer] WHERE [CreatedAt] < @cutoff", new { cutoff });
```

## What is not here

No joins or LINQ (write the SQL), no paging helper, no streaming (a query reads all rows into a list),
no multi-result sets, no change tracking. A column is always read with the **exact** type of the property:
see [Type mapping](type-mapping.md#reading-tables-the-library-did-not-create).

## Cancellation and transactions

The `DbConnection` methods have overloads taking a `CancellationToken` and an optional `DbTransaction`.
`IDatabase` methods take neither. See [Transactions](transactions.md).

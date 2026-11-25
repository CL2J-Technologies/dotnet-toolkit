# Parameters and the plan cache

Every value goes to SQL Server as a **parameter**; none is ever concatenated into the statement text
(`NoInlinedValuesTests`). That is what protects you from SQL injection.

How a parameter is **declared** matters as well. `SqlClient` infers a type and a length from each value, and
a different length produces a different parameter signature, which means another cached plan. The library
declares parameters from the entity's column instead of from the value, so the plan cache stays small and the
index stays usable.

| Value | Declared as | Why |
|---|---|---|
| `string` | `nvarchar(4000)`, or `nvarchar(max)` above 4000 characters | One signature whatever the length. |
| `string` bound to a **key** column (any `KeyType` other than `None`), in the statements the library builds from an entity | `varchar(8000)`, or `varchar(max)` above 8000 characters | An `nvarchar` parameter against a `varchar` key column converts the column and defeats the index (the source measures 34,235 logical reads against 3 on a million rows). Not applied when the column's `TypeName` is `nvarchar`/`nchar`/`ntext`, and never to non-key columns: free text stays Unicode so nothing is lost. |
| `DateTime` | `datetime2` | The default `datetime` loses precision and range. Not applied when `TypeName` names another type. |
| `decimal` of scale 18 or less and magnitude below 10^10 | `decimal(38,18)` | One signature for every amount. Any other value keeps the provider's own declaration, so it is never truncated. |
| a `null` | the type of the property | A bare `NULL` has no type, so a null would otherwise get its own plan. |

Proven by `ParameterDeclarationPlanTests`, `ParameterTypingPlanTests`, `ParameterDeclarationTests`,
`ParameterDeclarationNullTests` and `ParameterTypingTests`: the first two count the distinct plans the server
actually caches.

## Effect on your code

- Nothing to configure; this is the default behaviour.
- A raw `Query<T>(sql, new { ... })` gets the same declarations from the **property types** of the anonymous
  object: a `string` is `nvarchar`, a `DateTime` is `datetime2`.

## Anonymous parameters and varchar keys

The `varchar` binding needs to know which column a value belongs to. It is applied to:

- `QueryKey<T>`, `QueryKeys<T>` and `DeleteKey<T>`, which bind the key column of `T`;
- the parameters of `Insert`, `Update` and `Delete` of an entity, which come from the `[Column]` attributes of `T`.

It is **not** applied to `Query`, `QuerySingle` and `Exists` (and their `IDatabase` equivalents) called with an
anonymous parameter object such as `new { Id = id }`, nor to raw SQL. An anonymous type carries no `[Column]`, so the
library cannot know that the column is a `varchar` key, and the string is sent as `nvarchar`. The library does not
infer it from the key columns of `T` either: the same parameter name may refer to an `nvarchar` column in the SQL you
wrote, and binding it as ANSI would silently lose non-Latin characters. So the caller decides, in SQL, by casting to
the column's own type and length.

On SQL Server, comparing a `varchar` column with an `nvarchar` parameter converts the column, so the index seek
becomes a scan. It is invisible on small tables; on a million-row table a single-row lookup measured about 34,000
logical reads instead of 3.

```csharp
// nvarchar parameter against a varchar(36) key: the index is not used
var slow = await database.QuerySingle<Customer>("SELECT * FROM [Customer] WHERE [Id] = @id", new { id });

// the cast makes the parameter varchar: index seek
var fast = await database.QuerySingle<Customer>(
    "SELECT * FROM [Customer] WHERE [Id] = CAST(@id AS varchar(36))", new { id });

// no cast needed: the key column is known
await using var connection = await database.CreateConnection();
var customer = await connection.QueryKey<Customer>(id);
```

A query by example (`QuerySingle<T>(new { Id = id })`, no SQL) has nothing to cast in: use `QueryKey`, or write the
SQL with the cast.

Explained in [#43](https://github.com/CL2J-Technologies/dotnet-toolkit/issues/43). Alternatively, declare the key
column `nvarchar` with `TypeName`.

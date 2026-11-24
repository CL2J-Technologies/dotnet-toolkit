# Schema

The library creates and drops tables from your classes. It is a convenience for tests, tools and simple
applications, **not a migration system**.

```cs
await using var connection = await database.CreateConnection();

bool exists = await connection.TableExists<Customer>();
await connection.CreateTable<Customer>();              // fails if the table already exists
await connection.CreateTableIfRequired<Customer>();    // creates it only when TableExists is false
await connection.DropTable<Customer>();                // fails if the table is missing
await connection.DropTableIfExists<Customer>();
```

Each also has a `Type`-based overload taking a `CancellationToken` and an optional `DbTransaction`:
`connection.CreateTable(typeof(Customer), cancellationToken, transaction)`.

- `TableExists` asks `INFORMATION_SCHEMA.TABLES` for the table name and schema (`dbo` when none is given).
  Errors (lost connection, timeout, no permission) propagate: they are never reported as "does not exist".
  (`ExistenceTests`.)
- `DropTable` on a missing table throws; use `DropTableIfExists`.
- **`CreateTable` does not create the schema.** A `[Table(Schema = "billing")]` fails with
  `The specified schema name "billing" either does not exist...` until you run `CREATE SCHEMA billing`
  yourself.
- Nothing is altered or compared for an existing table. A changed class does not change its table.

## What `CreateTable` generates

For this entity:

```cs
[Table("Invoice", Schema = "billing")]
public class Invoice
{
    [Column(Name = "Id", Key = KeyType.Key, Length = 50)]
    public string Id { get; set; } = null!;

    [Column(Name = "CustomerId", Length = 50)]
    [ForeignKey("FK_Invoice_Customer", "Customer", "Id")]
    public string? CustomerId { get; set; }

    [Column(Name = "Amount", Length = 18, Decimals = 2, Required = true)]
    public decimal Amount { get; set; }

    [Column(Name = "Metadata")]                       // no Length: varchar(max)
    public string? Metadata { get; set; }
}
```

it emits (`StatementGenerationTests.Create_table_declares_columns_constraints_and_the_foreign_key`):

```sql
CREATE TABLE [billing].[Invoice]
([Id] varchar(50) NOT NULL, CONSTRAINT [PK_Invoice] PRIMARY KEY CLUSTERED([Id] ASC),
 [CustomerId] varchar(50), CONSTRAINT [FK_Invoice_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [Customer]([Id]),
 [Amount] decimal(18,2) NOT NULL, [Metadata] varchar(max))
```

- Column types: see [Type mapping](type-mapping.md). `TypeName` overrides it.
- `NOT NULL` comes from `Required = true`, from a key, or from an enum. A C# nullable annotation is not
  read.
- `Default` is appended as `DEFAULT <expression>` (raw SQL, e.g. `Default = "SYSUTCDATETIME()"`).
  See [Edge cases](edge-cases.md#default-is-ddl-only): the library's own `Insert` never relies on it.
- Names are wrapped in `[...]` **but not escaped**. Table, schema, column and constraint names must come from your
  code, never from user input.

## Foreign keys

`[ForeignKey(name, referenceTable, referenceField, referenceSchema)]` on a property adds a named
`FOREIGN KEY` constraint to `CreateTable`. `referenceSchema` is optional.

- The referenced table must exist first: create tables in dependency order, drop them in reverse.
- The referencing column must have the same type and length as the referenced one (a server rule): a
  `varchar(50)` column cannot reference a `varchar(36)` key.
- The constraint text is proven by unit tests (`StatementGenerationTests`, `DescriptorTests`). Creating the
  table and having SQL Server enforce the constraint was **observed** against SQL Server 2022 while writing
  these docs; no integration test covers it. The library does not read or follow foreign keys in queries:
  there are no joins or navigation properties.

## Not generated

Indexes (other than the primary key), unique constraints, check constraints and computed columns. Create those
with `Execute("CREATE INDEX ...")` after `CreateTable`.

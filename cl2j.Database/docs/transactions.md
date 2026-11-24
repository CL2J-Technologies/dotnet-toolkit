# Transactions

Transactions live on the connection. `IDatabase` opens a connection per call, so it cannot take part in
one.

```cs
await using var connection = await database.CreateConnection();

await using (var transaction = await connection.BeginTransactionAsync())
{
    var id = await connection.NewKey<Order>();           // SelfGeneratedKey entity

    await connection.Insert(new Order { Id = id, Total = 10m }, CancellationToken.None, transaction);
    await connection.Update(existing, CancellationToken.None, transaction);
    await connection.DeleteKey<Order>(oldId, CancellationToken.None, transaction);

    await transaction.CommitAsync();
}   // disposed without a commit: rolled back
```

- `BeginTransactionAsync` is the standard `DbConnection` method; the library does not wrap it.
- The write methods (`Insert`, `Update`, `UpdateColumn`, `Delete`, `DeleteKey`, `Execute`, `BulkInsert`),
  the queries and the DDL methods all have an overload ending in `CancellationToken, DbTransaction? transaction`.
  You pass the transaction **explicitly** on every call that should take part.
- A call that does not receive the transaction is not part of it. Forgetting the argument is not
  caught by the library.
- A rollback undoes every statement that was given the transaction (`A_rollback_undoes_every_statement_of_the_transaction`).

## What is covered by tests

Commit and rollback of `Insert`, `Update` and `DeleteKey`, always with `CancellationToken.None`
(`TransactionTests`). The overloads for `BulkInsert`, `UpdateColumn`, `Execute` and the queries accept a
transaction too but have **no test** of their own (a `BulkInsert` rolled back with its transaction was observed
to leave no rows).

## Limits

- No ambient `TransactionScope` support is documented or tested.
- No nested transactions or savepoints in the API; use `DbTransaction`/`SqlTransaction` directly.
- `IDatabase` methods take neither a transaction nor a cancellation token.

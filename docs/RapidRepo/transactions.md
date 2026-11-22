# Transactions

Each `Commit` / `CommitAsync` is its own transaction. To make several commits succeed or fail together, run them in an explicit transaction through the unit of work, without touching the provider's own types such as `DbContext`.

The API is part of `IUnitOfWork<TUserKey>` and is provider-neutral. Units of work that derive from `EfUnitOfWork<TUserKey>`, including generated ones, support it without any code change. A provider that doesn't support transactions throws `NotSupportedException`; see [Implementing it in a provider](#implementing-it-in-a-provider).

---

## Running work in a transaction

`ExecuteInTransactionAsync` runs a delegate in a transaction. The transaction commits when the delegate completes and rolls back when it throws; the exception is rethrown unchanged.

```csharp
await unitOfWork.ExecuteInTransactionAsync(async ct =>
{
    await unitOfWork.Orders.AddAsync(order, ct);
    await unitOfWork.CommitAsync(cancellationToken: ct);   // order.Id is now set

    foreach (var line in lines) line.OrderId = order.Id;
    await unitOfWork.OrderLines.AddRangeAsync(lines, ct);
    await unitOfWork.CommitAsync(cancellationToken: ct);
}, cancellationToken);
```

- The delegate commits the unit of work itself, as often as it needs. `ExecuteInTransactionAsync` takes no `userId` and doesn't commit for you, so `CommitAsync` behaves the same inside and outside a transaction.
- An overload returns the delegate's result: `var id = await unitOfWork.ExecuteInTransactionAsync(async ct => { ...; return order.Id; });`.
- Pass an `isolationLevel` (`System.Data.IsolationLevel`) to choose one. Without it, the database's default applies.

### Retries: stage inside the delegate

A provider may run the delegate again after a transient failure. With EF Core this happens with an execution strategy that retries on failure, such as `EnableRetryOnFailure`; see [EF Core — Transactions](../RapidRepo.EntityFrameworkCore/entity-framework-core.md#transactions). Before a retry, the unit of work forgets what the failed attempt staged, so create and stage entities **inside** the delegate. Changes staged before the call are lost if the delegate is retried.

---

## Nested calls join the outer transaction

Called while a transaction is open, `ExecuteInTransactionAsync` and `BeginTransactionAsync` join it instead of opening a second one. The outer transaction decides the outcome, so services that each use a transaction can call one another:

```csharp
await unitOfWork.ExecuteInTransactionAsync(async ct =>
{
    await orders.PlaceAsync(order, ct);      // uses ExecuteInTransactionAsync itself: joins this one
    await invoices.CreateAsync(order, ct);   // if this throws, the order is rolled back too
});
```

A joined `BeginTransactionAsync` returns `JoinedTransaction.Instance`, whose `CommitAsync`, `RollbackAsync` and disposal do nothing. To roll joined work back, let the exception reach the outer transaction.

A joining call runs at the outer transaction's isolation level. If it asks for a stricter `isolationLevel` than the outer transaction provides, it throws `InvalidOperationException` rather than quietly running with weaker guarantees. `ReadUncommitted`, `ReadCommitted`, `RepeatableRead` and `Serializable` are ordered from weakest to strictest; `Snapshot` only satisfies a request for `Snapshot`. Open the outer transaction at the strictest level the work inside it needs.

---

## Opening a transaction by hand

When a delegate doesn't fit, for example a transaction that spans a test or a middleware pipeline, open one with `BeginTransactionAsync`:

```csharp
await using var transaction = await unitOfWork.BeginTransactionAsync();

await unitOfWork.Notes.AddAsync(note);
await unitOfWork.CommitAsync();

await transaction.CommitAsync();   // disposing without CommitAsync rolls back
```

This form is never retried. With EF Core and a strategy that retries on failure it throws `InvalidOperationException`; use `ExecuteInTransactionAsync` there.

---

## After a rollback, discard the unit of work

Commits made inside a rolled-back transaction have already changed the entities in memory: generated keys, [audit fields](auditing.md), and the incremented `Version` of [versioned entities](concurrency.md). Those no longer match the database. Discard the unit of work and its entities after a failed transaction, and reload in a new one (a new DI scope) to try again, as after a [concurrency conflict](concurrency.md#handling-a-conflict).

A `ConcurrencyException` inside the delegate rolls back the whole transaction, including earlier commits, and is not retried, because it isn't transient.

---

## Implementing it in a provider

A provider's obligations, and the conformance tests that check them, are in [Writing a provider — Transactions](providers.md#transactions).

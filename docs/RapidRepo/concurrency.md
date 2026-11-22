# Optimistic Concurrency

Implement `IVersionedEntity` to stop concurrent edits from overwriting each other. A commit that would update or delete a row that someone else changed since it was read throws `ConcurrencyException` (namespace `RapidRepo.Concurrency`), and nothing in that commit is saved on a store with transactions, such as any relational database (EF Core InMemory has none, so changes written before the conflict stay written there). Entities that don't implement the interface keep last-write-wins behaviour.

The rules on this page are provider-neutral. With EF Core, also call `configurationBuilder.AddConcurrencyTokens()` in `ConfigureConventions`; see [Optimistic concurrency](../RapidRepo.EntityFrameworkCore/entity-framework-core.md#optimistic-concurrency).

---

## Making an entity versioned

```csharp
public class Order : BaseAuditableEntity<int, Guid>, IVersionedEntity
{
    public string Status { get; set; } = "";
    public long Version { get; set; }
}

// Or, without auditing:
public class Setting : BaseVersionedEntity<int>
{
    public string Value { get; set; } = "";
}
```

### Versioning every entity

`IVersionedEntity` is opt-in. To version every entity in an application, add it to your application-level base class (see [Reducing repetition with application-level base classes](entities.md#reducing-repetition-with-application-level-base-classes)):

```csharp
public abstract class AppEntity<TId> : BaseAuditableDeletableEntity<TId, Guid>, IVersionedEntity
    where TId : notnull
{
    public long Version { get; set; }
}
```

---

## How the version changes

`Version` is managed by the library. At commit, the unit of work checks that each updated or deleted row still has the version the entity was read at, and increments `Version` on every update:

| Change | Version |
|---|---|
| Added | Kept as given (`0` by default) |
| Modified, soft-deleted or restored | Checked, then incremented |
| Hard-deleted | Checked |

After a successful commit, the entity holds the new version, so you can return it to the client.

The check identifies the row by its key and version. A store that reuses the key of a deleted versioned entity can let a stale update or delete match the new row once it reaches the same version, so don't reuse the keys of versioned entities. Most generated keys are never reused, but some stores recycle them, such as SQLite's `INTEGER PRIMARY KEY` without `AUTOINCREMENT`.

Don't increment `Version` yourself. Writes made outside the unit of work, such as raw SQL or bulk updates, don't change it either. Increment it there by hand, or concurrent edits won't notice those writes.

---

## Updates from a client

The entity's current `Version` is the version the unit of work expects the store to still hold. Send `Version` to the client with the entity, like an HTTP `ETag`, and copy it back onto the entity before you update it:

```csharp
// Detached: build the entity from the request.
var order = new Order { Id = id, Status = request.Status, Version = request.Version };
repo.Update(order);
await unitOfWork.CommitAsync();   // throws ConcurrencyException if the order changed since request.Version
return order.Version;             // the new version for the client
```

```csharp
// Loaded: read the entity, then apply the client's version along with its changes.
var order = await repo.GetByIdAsync(id);
order.Status = request.Status;
order.Version = request.Version;
repo.Update(order);
await unitOfWork.CommitAsync();
```

---

## Handling a conflict

`ConcurrencyException.Entities` lists the conflicting entities, and `InnerException` holds the provider's exception. After a failed commit, the entities carry the version they were read at again.

The unit of work still holds the stale entities, so committing it again fails the same way. To retry, reload in a new unit of work (a new DI scope), apply the change again, and commit. Or report the conflict to the caller, for example as HTTP 409 or 412:

```csharp
try
{
    await unitOfWork.CommitAsync();
}
catch (ConcurrencyException)
{
    return Results.Conflict("The order was changed by someone else. Reload it and try again.");
}
```

Inside a [transaction](transactions.md), a conflict rolls back every commit the transaction made, not only the one that failed.

---

## Implementing it in a provider

A provider's obligations, and the conformance tests that check them, are in [Writing a provider — Optimistic concurrency](providers.md#optimistic-concurrency).

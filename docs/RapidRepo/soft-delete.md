# Soft Delete

A soft-deleted entity stays in the store with its `DeletedAt` set, and reads skip it. It can be restored, or removed for good. This page covers the whole lifecycle; the rules are provider-neutral, and each provider documents how it hides deleted rows (for EF Core, see [Soft delete and query filters](../RapidRepo.EntityFrameworkCore/entity-framework-core.md#soft-delete-and-query-filters)).

---

## Making an entity soft-deletable

Inherit from `BaseAuditableDeletableEntity<TId>` or `BaseAuditableDeletableEntity<TId, TUserKey>` (see [Entities](entities.md#baseauditabledeletableentitytid--baseauditabledeletableentitytid-tuserkey)), or implement `IDeletableEntity` / `IDeletableEntity<TUserKey>` yourself and declare its properties:

```csharp
// Timestamp only: declare DeletedAt to satisfy IDeletableEntity
public class Product : BaseAuditableEntity<long>, IDeletableEntity
{
    public string Name { get; set; } = string.Empty;
    public DateTime? DeletedAt { get; set; }
}

// With user tracking: declare both DeletedAt and DeletedBy
public class Product : BaseAuditableEntity<long, Guid>, IDeletableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
```

Don't set `DeletedAt` or `DeletedBy` yourself. The unit of work stamps them at commit; see [Auditing](auditing.md#how-auditing-works).

### Inheritance

In an inheritance hierarchy, implement the interface on the root type, so that every type below it is soft-deletable too:

```csharp
public abstract class Document : BaseEntity<int>, IDeletableEntity
{
    public DateTime? DeletedAt { get; set; }
}

public class Invoice : Document { }   // soft-deletable through Document
public class Contract : Document { }  // soft-deletable through Document
```

EF Core only allows query filters on the root, so with EF Core a deletable type under a root that is not deletable cannot be filtered, and `AddSoftDeleteQueryFilters()` throws; see [Inheritance](../RapidRepo.EntityFrameworkCore/entity-framework-core.md#inheritance).

---

## Deleting

For an `IDeletableEntity`, `Delete` and `DeleteById` mark the entity as deleted instead of removing the row. For any other entity they remove it permanently.

```csharp
// Soft delete: the row stays in the table.
_unitOfWork.Products.Delete(product);
await _unitOfWork.CommitAsync();
```

At commit, the unit of work stamps `DeletedAt`, `DeletedBy` (for `IDeletableEntity<TUserKey>`), and `ModifiedAt` / `ModifiedBy`. Only the transition from not deleted to deleted stamps them: deleting an entity that is already deleted, or editing it, keeps who deleted it and when.

---

## Reading deleted entities

Reads exclude soft-deleted entities by default. Pass `IncludeDeleted` to see them:

```csharp
var includeDeleted = new ReadOptions { IncludeDeleted = true };

var all = await _unitOfWork.Products.GetAllAsync(options: includeDeleted);
Product? deleted = await _unitOfWork.Products.GetByIdAsync(id, includeDeleted);
bool existsEvenIfDeleted = await _unitOfWork.Products.ExistsByIdAsync(id, includeDeleted);
int total = await _unitOfWork.Products.CountAsync(options: includeDeleted);
```

`IncludeDeleted` concerns soft delete only. A provider that cannot tell its soft-delete filter apart from other filters may bypass those too; see the provider's documentation. With EF Core and `AddSoftDeleteQueryFilters()`, other named filters such as a tenant filter stay in force.

---

## Restoring and permanently removing

| Method | What it does |
|---|---|
| `Restore(entity)` / `RestoreById(id)` | Clears `DeletedAt` (and `DeletedBy` when the entity implements `IDeletableEntity<TUserKey>`), making the record visible again |
| `HardDelete(entity)` / `HardDeleteById(id)` | Removes the row permanently, bypassing soft delete |

```csharp
// Undo a soft delete. RestoreById finds deleted entities by default, and does nothing when there is none.
_unitOfWork.Products.RestoreById(productId);
await _unitOfWork.CommitAsync();

// Purge it for good.
_unitOfWork.Products.HardDelete(product);
await _unitOfWork.CommitAsync();

// By identifier, HardDeleteById only reaches visible rows by default.
// includeDeleted: true is what reaches a row that is already soft-deleted.
_unitOfWork.Products.HardDeleteById(productId, includeDeleted: true);
await _unitOfWork.CommitAsync();
```

- Both come in range forms (`RestoreRange`, `HardDeleteRange`). Like `Delete` and `Update`, they only stage a change, so they have no async counterparts. Only the `*ById` variants look the entity up, and only those have `*Async` versions.
- `RestoreById` defaults `includeDeleted` to `true`, because the entity to restore is deleted. `HardDeleteById` defaults it to `false`, because the operation cannot be undone.
- A restore is an ordinary modification, so `ModifiedAt` and `ModifiedBy` are stamped on commit.
- An entity already staged for removal by `HardDelete` is revived by `Restore`, so the restore is not silently discarded.
- `Restore` throws `InvalidOperationException` for an entity that does not implement `IDeletableEntity`; a hard-deleted row is gone.
- `HardDelete` works on any entity. For one without soft delete it does exactly what `Delete` does.
- For an [`IVersionedEntity`](concurrency.md), a soft delete and a restore are updates, so they check and increment `Version`.

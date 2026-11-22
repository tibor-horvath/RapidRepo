# API Reference

The contracts below are provider-neutral. EF Core specifics, such as includes, tracking and `IQueryable`, travel in `EfReadOptions<TEntity>`. See [EF Core read options](../RapidRepo.EntityFrameworkCore/entity-framework-core.md#read-options).

---

## Read methods — `IReadOnlyRepository<TEntity, TKey>`

Every read has an async variant (`GetByIdAsync`, `GetAllAsync`, …) that also takes a `CancellationToken`.

### GetById

Returns the entity with the given key, or `null` if not found.

```csharp
Product? product = await _unitOfWork.Products.GetByIdAsync(id);

// Projected
string? name = await _unitOfWork.Products.GetByIdAsync(id, selector: p => p.Name);

// Including a soft-deleted entity
Product? deleted = await _unitOfWork.Products.GetByIdAsync(id, new ReadOptions { IncludeDeleted = true });
```

### GetByIds

Returns the entities whose keys are in the collection. Keys without an entity are skipped, and an empty collection returns an empty result without querying the store.

```csharp
IEnumerable<Product> products = await _unitOfWork.Products.GetByIdsAsync(ids);
IEnumerable<string> names = await _unitOfWork.Products.GetByIdsAsync(ids, selector: p => p.Name);
```

### GetAll

Returns all entities matching the optional condition, in the optional sort order.

```csharp
IEnumerable<Product> all = await _unitOfWork.Products.GetAllAsync();

IEnumerable<Product> active = await _unitOfWork.Products.GetAllAsync(
    condition: p => p.IsActive,
    orderBy: SortOrder<Product>.By(p => p.Name));
```

### GetAllPaged

Returns a `Paged<TEntity>` (namespace `RapidRepo.Dtos`), or a `Paged<TResult>` when a selector is supplied. The page index is 1-based. **`pageIndex` defaults to `1` and `pageSize` to `10`**, so a call without them returns only the first 10 rows.

```csharp
Paged<Product> page = await _unitOfWork.Products.GetAllPagedAsync(
    condition: p => p.IsActive,
    orderBy: SortOrder<Product>.By(p => p.Name),
    pageIndex: 1,
    pageSize: 20);

Paged<ProductSummary> summaries = await _unitOfWork.Products.GetAllPagedAsync(
    selector: p => new ProductSummary(p.Id, p.Name, p.Price),
    condition: p => p.IsActive,
    pageIndex: 1,
    pageSize: 20);
```

**Without a sort order, paging orders by `Id`**, so every provider returns the same, stable pages.

| `Paged<T>` property | Description |
|---|---|
| `Results` | Items on the current page |
| `TotalCount` | Total matching records |
| `Page` | Current page index |
| `PageSize` | Items per page |
| `TotalPages` | Total number of pages |
| `HasNext` | Whether a next page exists |
| `HasPrevious` | Whether a previous page exists |

`pageIndex` and `pageSize` must be at least 1; lower values throw `ArgumentOutOfRangeException`. `T` must be a reference type.

### GetFirst / GetFirstOrDefault

`GetFirst` throws `InvalidOperationException` when nothing matches; `GetFirstOrDefault` returns `null`. Pass a sort order to decide which entity is first.

```csharp
Product cheapest = await _unitOfWork.Products.GetFirstAsync(
    condition: p => p.Price > 10m,
    orderBy: SortOrder<Product>.By(p => p.Price));

Product? firstOrNull = await _unitOfWork.Products.GetFirstOrDefaultAsync(p => p.Price > 10m);
```

### GetSingle / GetSingleOrDefault

`GetSingle` throws when nothing, or more than one entity, matches. `GetSingleOrDefault` returns `null` when nothing matches and throws when more than one does.

```csharp
Product single = await _unitOfWork.Products.GetSingleAsync(p => p.Sku == "ABC-123");
Product? singleOrNull = await _unitOfWork.Products.GetSingleOrDefaultAsync(p => p.Sku == "ABC-123");
```

### ExistsById / Any / Count

```csharp
bool exists = await _unitOfWork.Products.ExistsByIdAsync(id);
bool any = await _unitOfWork.Products.AnyAsync(p => p.Sku == "ABC-123");
int count = await _unitOfWork.Products.CountAsync(p => p.IsActive);
```

---

### Common parameters

| Parameter | Type | Default | Description |
|---|---|---|---|
| `condition` | `Expression<Func<TEntity, bool>>?` | `null` | Filter predicate |
| `orderBy` | `SortOrder<TEntity>?` | `null` | Sort order |
| `options` | `ReadOptions?` | `null` (`ReadOptions.Default`) | Read options; providers accept their own subclass |
| `pageIndex` / `pageSize` | `int` | `1` / `10` | Paging, on `GetAllPaged` only |
| `selector` | `Expression<Func<TEntity, TResult>>` | — | Projection, on the selector overloads |

Which methods take which parameters:

| Methods | `condition` | `orderBy` | `options` |
|---|---|---|---|
| `GetAll`, `GetAllPaged`, `GetFirst*` | ✓ | ✓ | ✓ |
| `GetSingle*` | ✓ | — | ✓ |
| `GetById`, `GetByIds`, `ExistsById` | — | — | ✓ |
| `Any` | required | — | ✓ |
| `Count` | ✓ | — | ✓ |

### `SortOrder<TEntity>`

A declarative sort order, so any provider can translate it, whether to LINQ or to SQL `ORDER BY`. It is immutable; each `ThenBy` returns a new order.

```csharp
var order = SortOrder<Product>.By(p => p.Category).ThenByDescending(p => p.Price).ThenBy(p => p.Name);
```

| Member | Description |
|---|---|
| `By(key)` / `ByDescending(key)` | Starts an order on `key` |
| `ThenBy(key)` / `ThenByDescending(key)` | Adds a less significant key |
| `Keys` | The keys, most significant first, as `SortKey(LambdaExpression Key, bool Descending)` |

### `ReadOptions`

| Member | Default | Description |
|---|---|---|
| `IncludeDeleted` | `false` | Also return soft-deleted entities |
| `ReadOptions.Default` | — | A shared instance with the defaults |

A provider may accept a subclass with its own hints, for example `EfReadOptions<TEntity>`. A provider that receives another provider's options uses only the `ReadOptions` members and ignores the rest.

### Selector overloads

`GetAll`, `GetAllPaged`, `GetFirst*`, `GetSingle*`, `GetById` and `GetByIds` all have a `selector` overload that returns a projection instead of the entity. With Entity Framework Core the projection runs in the database, so only the selected columns are read; a provider that cannot translate selectors may load the matching rows and project them in memory (see [Writing a provider](providers.md#reads)):

```csharp
IEnumerable<ProductSummary> summaries = await _unitOfWork.Products.GetAllAsync(
    selector: p => new ProductSummary(p.Id, p.Name, p.Price),
    condition: p => p.IsActive);
```

The projection overloads that also take a condition (`GetAll`, `GetAllPaged`, `GetFirst*`, `GetSingle*`) require `TResult` to be a reference type. Without that constraint, `GetFirstOrDefault(p => p.IsActive)` would be ambiguous between a condition and a `bool` projection. Project scalars through an anonymous type or a record, for example `p => new { p.Id }`. `GetById` and `GetByIds` projections may return scalars.

---

## Write methods — `IWriteRepository<TEntity, TKey>`

| Method | Description |
|---|---|
| `Add(entity)` / `AddAsync(entity)` | Stages an entity for insertion |
| `AddRange(entities)` / `AddRangeAsync(entities)` | Stages several entities for insertion |
| `Update(entity)` / `UpdateRange(entities)` | Stages an update |
| `Delete(entity)` / `DeleteRange(entities)` | Soft-deletes an `IDeletableEntity`; permanently removes anything else |
| `DeleteById(id)` / `DeleteByIdAsync(id)` | Looks the entity up, then calls `Delete`. Does nothing when no entity has that key |
| `Restore(entity)` / `RestoreRange(entities)` | Clears `DeletedAt`, and `DeletedBy` when the entity tracks it |
| `RestoreById(id, includeDeleted = true)` / `RestoreByIdAsync` | Looks the entity up, then calls `Restore` |
| `HardDelete(entity)` / `HardDeleteRange(entities)` | Permanently removes, bypassing soft delete |
| `HardDeleteById(id, includeDeleted = false)` / `HardDeleteByIdAsync` | Looks the entity up, then removes it permanently |

- All write methods only stage changes. Call `Commit` or `CommitAsync` on the unit of work to persist them.
- The `Restore` methods throw `InvalidOperationException` when `TEntity` does not implement `IDeletableEntity`.
- The by-id methods also find an entity staged for insertion but not yet committed.
- See [Soft Delete](soft-delete.md) for how `Delete`, `Restore` and `HardDelete` work together.
- `includeDeleted` decides whether the lookup finds soft-deleted entities. `RestoreById` defaults it to `true`, because the entity to restore is deleted. `HardDeleteById` defaults it to `false`, because the operation cannot be undone.

---

## Unit of Work — `IUnitOfWork<TUserKey>`

| Method | Description |
|---|---|
| `CommitAsync(userId?, cancellationToken)` | Stamps audit fields and versions, and persists every staged change. Throws `ConcurrencyException` on a conflict of an `IVersionedEntity` |
| `Commit(userId?)` | Synchronous version of `CommitAsync` |
| `ExecuteInTransactionAsync(operation, isolationLevel?, cancellationToken)` | Runs `operation` in a transaction: commits it when `operation` completes, rolls it back when it throws. An overload returns `operation`'s result. Joins a transaction that is already open |
| `BeginTransactionAsync(isolationLevel?, cancellationToken)` | Opens a transaction by hand and returns an `IUnitOfWorkTransaction` (`CommitAsync`, `RollbackAsync`; disposing without commit rolls back). Never retried |

Without a `userId`, the audit user comes from the `ICurrentUserProvider<TUserKey>`, if any, and otherwise is the default user key. See [Auditing](auditing.md) for the audit rules and [the current user](auditing.md#the-current-user), [Optimistic Concurrency](concurrency.md) for conflicts, and [Transactions](transactions.md) for grouping commits. A unit of work whose provider doesn't support transactions throws `NotSupportedException` from the transaction members.

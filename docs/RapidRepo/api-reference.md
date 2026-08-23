# API Reference

---

## Read methods — `IReadOnlyRepository<TEntity, TKey>`

### GetById

Returns the entity with the given primary key, or `null` if not found.

```csharp
Product? product = await _unitOfWork.Products.GetByIdAsync(id);

// With eager-loading
Product? product = await _unitOfWork.Products.GetByIdAsync(id,
    include: q => q.Include(p => p.Category));

// Projected to a DTO
string? name = await _unitOfWork.Products.GetByIdAsync(id,
    selector: p => p.Name);
```

### GetAll

Returns all entities matching the optional condition.

```csharp
IEnumerable<Product> all = await _unitOfWork.Products.GetAllAsync();

IEnumerable<Product> active = await _unitOfWork.Products.GetAllAsync(
    condition: p => p.IsActive,
    orderBy: q => q.OrderBy(p => p.Name));
```

### GetAllPaged

Returns a `Paged<TEntity>`, or `Paged<TResult>` when a selector is supplied. Page index is 1-based.

```csharp
Paged<Product> page = await _unitOfWork.Products.GetAllPagedAsync(
    condition: p => p.IsActive,
    orderBy: q => q.OrderBy(p => p.Name),
    pageIndex: 1,
    pageSize: 20);

// Projected to a DTO — returns Paged<ProductSummary>
Paged<ProductSummary> summaries = await _unitOfWork.Products.GetAllPagedAsync(
    selector: p => new ProductSummary(p.Id, p.Name, p.Price),
    condition: p => p.IsActive,
    orderBy: q => q.OrderBy(p => p.Name),
    pageIndex: 1,
    pageSize: 20);
```

| `Paged<T>` property | Description |
|---|---|
| `Results` | Items on the current page |
| `TotalCount` | Total matching records |
| `Page` | Current page index |
| `PageSize` | Items per page |
| `TotalPages` | Total number of pages |
| `HasNext` | Whether a next page exists |
| `HasPrevious` | Whether a previous page exists |

> `T` must be a reference type (`class`). Using a value type such as `int` or `bool` as the paged item type is a compile error.

### GetFirst / GetFirstOrDefault

`GetFirst` throws if no match is found. `GetFirstOrDefault` returns `null`.

```csharp
Product first = await _unitOfWork.Products.GetFirstAsync(
    condition: p => p.Price > 10m,
    orderBy: q => q.OrderBy(p => p.Price));

Product? firstOrNull = await _unitOfWork.Products.GetFirstOrDefaultAsync(
    condition: p => p.Price > 10m);
```

### GetSingle / GetSingleOrDefault

`GetSingle` throws if zero or more than one match is found. `GetSingleOrDefault` returns `null` when no match is found.

```csharp
Product single = await _unitOfWork.Products.GetSingleAsync(
    condition: p => p.Sku == "ABC-123");

Product? singleOrNull = await _unitOfWork.Products.GetSingleOrDefaultAsync(
    condition: p => p.Sku == "ABC-123");
```

### ExistsById

Returns `true` if an entity with the given key exists. Respects global query filters (e.g. soft-deleted entities return `false`) unless `ignoreQueryFilters` is set.

```csharp
bool exists = await _unitOfWork.Products.ExistsByIdAsync(id);

// Include soft-deleted entities
bool existsDeleted = await _unitOfWork.Products.ExistsByIdAsync(id, ignoreQueryFilters: true);
```

### GetByIds

Returns all entities whose primary key is in the supplied collection. Respects global query filters by default.

```csharp
IEnumerable<Product> products = await _unitOfWork.Products.GetByIdsAsync(ids);

// Projected to a DTO
IEnumerable<string> names = await _unitOfWork.Products.GetByIdsAsync(ids, selector: p => p.Name);
```

### Any / Count

```csharp
bool exists = await _unitOfWork.Products.AnyAsync(p => p.Sku == "ABC-123");

int count = await _unitOfWork.Products.CountAsync(p => p.IsActive);
```

---

### Common optional parameters

#### `Get*` methods (`GetById`, `GetAll`, `GetAllPaged`, `GetFirst`, `GetFirstOrDefault`, `GetSingle`, `GetSingleOrDefault`)

| Parameter | Type | Default | Description |
|---|---|---|---|
| `condition` | `Expression<Func<TEntity, bool>>?` | `null` | Filter predicate |
| `orderBy` | `Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>?` | `null` | Sort function |
| `include` | `Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>?` | `null` | Eager-load navigation properties |
| `track` | `bool` | `true` | EF change tracking (set `false` for read-only queries) |
| `ignoreQueryFilters` | `bool` | `false` | Bypass global EF query filters |
| `useSplitQueries` | `bool` | `false` | Use [EF split queries](https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries) for includes |

#### `ExistsById` / `ExistsByIdAsync`

Accepts an `id` and an optional `ignoreQueryFilters` flag. No other filter parameters are supported.

#### `GetByIds` / `GetByIdsAsync`

Accepts an `IEnumerable<TKey> ids` and the standard `include`, `useSplitQueries`, `track`, and `ignoreQueryFilters` parameters. The `<TResult>` selector overload drops the `track` parameter (projection always uses `AsNoTracking`). `condition` and `orderBy` are **not** supported — use `GetAll` if you need additional filtering.

#### `Any` / `AnyAsync`

Only accepts a required `condition` predicate (and `cancellationToken` for the async variant). `orderBy`, `include`, `track`, `ignoreQueryFilters`, and `useSplitQueries` are **not** supported.

#### `Count` / `CountAsync`

Accepts an optional `condition` predicate and `ignoreQueryFilters`. `orderBy`, `include`, `track`, and `useSplitQueries` are **not** supported.

### Selector overloads

Every `GetAll`, `GetFirst`, `GetSingle`, `GetById`, `GetByIds`, and `GetAllPaged` method has a `selector` overload that projects the result without loading the full entity:

```csharp
// Returns IEnumerable<string> instead of IEnumerable<Product>
IEnumerable<string> names = await _unitOfWork.Products.GetAllAsync(
    selector: p => p.Name,
    condition: p => p.IsActive);

// Returns Paged<ProductSummary> instead of Paged<Product>
Paged<ProductSummary> page = await _unitOfWork.Products.GetAllPagedAsync(
    selector: p => new ProductSummary(p.Id, p.Name),
    pageIndex: 1,
    pageSize: 20);
```

> The `selector` overloads always use `AsNoTracking` internally — there is no `track` parameter on them.

---

## Write methods — `IWriteRepository<TEntity, TKey>`

| Method | Description |
|---|---|
| `Add(entity)` | Stages a single entity for insert |
| `AddRange(entities)` | Stages multiple entities for insert |
| `AddAsync(entity)` | Async version of `Add` |
| `AddRangeAsync(entities)` | Async version of `AddRange` |
| `Update(entity)` | Marks a single entity as modified |
| `UpdateRange(entities)` | Marks multiple entities as modified |
| `Delete(entity)` | Removes the entity, or sets `DeletedAt` if it implements `IDeletableEntity` |
| `DeleteRange(entities)` | Removes multiple entities, or sets `DeletedAt` on each if they implement `IDeletableEntity` |
| `DeleteById(id)` | Loads the entity by key then calls `Delete` |
| `DeleteByIdAsync(id)` | Async version of `DeleteById` |
| `Restore(entity)` | Clears `DeletedAt` — and `DeletedBy` when the entity implements `IDeletableEntity<TUserKey>` — undoing a soft delete |
| `RestoreRange(entities)` | Clears the deletion marks on multiple entities |
| `RestoreById(id, ignoreQueryFilters = true)` | Loads the entity by key then calls `Restore` |
| `RestoreByIdAsync(id, ignoreQueryFilters = true)` | Async version of `RestoreById` |
| `HardDelete(entity)` | Permanently removes the entity, bypassing soft delete |
| `HardDeleteRange(entities)` | Permanently removes multiple entities, bypassing soft delete |
| `HardDeleteById(id, ignoreQueryFilters = false)` | Loads the entity by key then removes it permanently |
| `HardDeleteByIdAsync(id, ignoreQueryFilters = false)` | Async version of `HardDeleteById` |

> The `Restore` methods throw `InvalidOperationException` when `TEntity` does not implement `IDeletableEntity` — there is no soft delete to undo. `HardDelete` is always available; for entities without soft delete it behaves exactly like `Delete`.

> **`ignoreQueryFilters: true` bypasses _every_ global query filter on the entity, not only the soft-delete one.** If your entity also carries a multi-tenancy filter (or anything else that scopes what the caller may see), `HardDeleteById(id, ignoreQueryFilters: true)` can permanently erase a row that belongs to another tenant. That is why `RestoreById` defaults it to `true` — a soft-deleted row is invisible otherwise, and the operation is reversible — while the destructive `HardDeleteById` defaults it to `false`. When a filter other than soft delete is in play, load the entity yourself and use `Restore(entity)` / `HardDelete(entity)`.

> `Restore`, `RestoreRange`, `HardDelete`, and `HardDeleteRange` have no async counterparts, for the same reason `Delete` and `Update` do not: they only stage a change in the EF change tracker. Only the `*ById` variants query the store, so only they are offered as `Task`-returning methods.

> `RestoreById` and `HardDeleteById` also reach an entity that has been staged for insertion but not yet committed, matching `DeleteById`. Everything already in the store is resolved by a query, so `ignoreQueryFilters` governs what those two can reach — an entity that merely happens to be tracked is *not* reachable when the filters exclude it. This differs from `DeleteById`, which uses EF's `Find` and therefore short-circuits on any tracked entity; that is harmless there, because for a soft-deletable entity `DeleteById` only sets `DeletedAt`.

> **Breaking change.** These members are declared on `IWriteRepository<TEntity, TKey>` itself, so any code that
> implements that interface by hand must add them. Repositories deriving from `WriteRepository<TEntity, TId>` or
> `BaseRepository<TEntity, TId>` inherit the implementations and need no changes, and generated mocks
> (Moq, NSubstitute, FakeItEasy) pick them up automatically. They were deliberately *not* given throwing default
> interface implementations: an interface member that compiles but fails at run time hides the problem instead of
> surfacing it, so the addition is shipped as a major version bump.

> All write methods only stage changes in the EF change tracker. Call `CommitAsync()` or `Commit()` on the Unit of Work to persist them. See [Unit of Work](unit-of-work.md).

---

## Unit of Work — `IUnitOfWork<TUserKey>`

| Method | Description |
|---|---|
| `CommitAsync(userId?, cancellationToken)` | Persists all staged changes and sets audit fields |
| `Commit(userId?)` | Synchronous version of `CommitAsync` |

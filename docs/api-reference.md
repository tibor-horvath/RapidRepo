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

Returns a `Paged<TEntity>` (or `Paged<TResult>` with a selector). Page index is 1-based.

```csharp
Paged<Product> page = await _unitOfWork.Products.GetAllPagedAsync(
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

#### `Any` / `AnyAsync`

Only accepts a required `condition` predicate (and `cancellationToken` for the async variant). `orderBy`, `include`, `track`, `ignoreQueryFilters`, and `useSplitQueries` are **not** supported.

#### `Count` / `CountAsync`

Accepts an optional `condition` predicate and `ignoreQueryFilters`. `orderBy`, `include`, `track`, and `useSplitQueries` are **not** supported.

### Selector overloads

Every `GetAll`, `GetFirst`, `GetSingle`, and `GetById` method has a `selector` overload that projects the result without loading the full entity:

```csharp
// Returns IEnumerable<string> instead of IEnumerable<Product>
IEnumerable<string> names = await _unitOfWork.Products.GetAllAsync(
    selector: p => p.Name,
    condition: p => p.IsActive);
```

---

## Write methods — `IWriteRepository<TEntity, TKey>`

| Method | Description |
|---|---|
| `Add(entity)` | Stages a single entity for insert |
| `AddRange(entities)` | Stages multiple entities for insert |
| `AddAsync(entity)` | Async version of `Add` |
| `AddAsync(entities)` | Async version of `AddRange` |
| `Update(entity)` | Marks a single entity as modified |
| `UpdateRange(entities)` | Marks multiple entities as modified |
| `Delete(entity)` | Marks a single entity for removal |
| `DeleteById(id)` | Marks the entity with the given key for removal |
| `DeleteRange(entities)` | Marks multiple entities for removal |

> All write methods only stage changes in the EF change tracker. Call `CommitAsync()` or `Commit()` on the Unit of Work to persist them. See [Unit of Work](unit-of-work.md).

---

## Unit of Work — `IUnitOfWork<TUserKey>`

| Method | Description |
|---|---|
| `CommitAsync(userId?, cancellationToken)` | Persists all staged changes and sets audit fields |
| `Commit(userId?)` | Synchronous version of `CommitAsync` |

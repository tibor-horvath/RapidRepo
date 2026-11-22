# Querying

Filtering, sorting, projection, paging and bulk writes. These patterns use only the provider-neutral contract; [API Reference](api-reference.md) lists every method and parameter. For EF Core specifics (eager loading, tracking, split queries, `IQueryable`, query filters), see [EF Core](../RapidRepo.EntityFrameworkCore/entity-framework-core.md).

---

## Filtering and sorting

Every multi-result query takes an optional `condition` and a `SortOrder<TEntity>`:

```csharp
var products = await _unitOfWork.Products.GetAllAsync(
    condition: p => p.IsActive && p.Price > 10m,
    orderBy: SortOrder<Product>.ByDescending(p => p.Price).ThenBy(p => p.Name));
```

Conditions are expression trees, which is what lets each provider translate them for its store.

---

## Projecting to a DTO (selector)

Use a `selector` expression to project results without loading the full entity:

```csharp
record ProductSummary(long Id, string Name, decimal Price);

var summaries = await _unitOfWork.Products.GetAllAsync(
    selector: p => new ProductSummary(p.Id, p.Name, p.Price),
    condition: p => p.IsActive,
    orderBy: SortOrder<Product>.By(p => p.Name));

// Scalars: through GetById / GetByIds directly ...
string? name = await _unitOfWork.Products.GetByIdAsync(id, selector: p => p.Name);

// ... or through an anonymous type for the overloads that also take a condition.
var ids = (await _unitOfWork.Products.GetAllAsync(selector: p => new { p.Id })).Select(x => x.Id);
```

Projections are never tracked by the provider.

---

## Bulk fetch by IDs

Use `GetByIdsAsync` when you have a set of known keys and want all matching entities in a single query:

```csharp
IEnumerable<Product> products = await _unitOfWork.Products.GetByIdsAsync(ids);

IEnumerable<ProductSummary> summaries = await _unitOfWork.Products.GetByIdsAsync(ids,
    selector: p => new ProductSummary(p.Id, p.Name, p.Price));
```

An empty `ids` collection returns an empty result immediately, without querying the store. Soft-deleted entities are excluded unless you pass `new ReadOptions { IncludeDeleted = true }`.

---

## Paged queries

`GetAllPagedAsync` returns a `Paged<T>` suitable for list views and APIs:

```csharp
Paged<Product> page = await _unitOfWork.Products.GetAllPagedAsync(
    condition: p => p.IsActive,
    orderBy: SortOrder<Product>.By(p => p.Name),
    pageIndex: 2,
    pageSize: 25);

Console.WriteLine($"Page {page.Page} of {page.TotalPages} ({page.TotalCount} total)");
```

**Without a sort order, paging orders by `Id`.** Pages are therefore stable and the same on every provider, which also satisfies databases that require an `ORDER BY` for offset paging.

Pass a `selector` to project each page item:

```csharp
Paged<ProductSummary> page = await _unitOfWork.Products.GetAllPagedAsync(
    selector: p => new ProductSummary(p.Id, p.Name, p.Price),
    condition: p => p.IsActive,
    orderBy: SortOrder<Product>.By(p => p.Name),
    pageIndex: 1,
    pageSize: 25);
```

> **Two queries:** paging counts the matching rows, then fetches the page. If you already know the total, for example from a cache, write a custom repository method that skips the count and builds `Paged<T>` itself.

---

## Soft-deleted entities in reads

Reads exclude soft-deleted entities by default. Pass `new ReadOptions { IncludeDeleted = true }` to see them; see [Soft Delete](soft-delete.md#reading-deleted-entities).

---

## Checking existence by ID

Use `ExistsByIdAsync` instead of `AnyAsync(e => e.Id == id)` when you only need to know whether an entity with a specific key exists:

```csharp
bool exists = await _unitOfWork.Products.ExistsByIdAsync(id);
```

---

## Bulk insert and update

```csharp
await _unitOfWork.Products.AddRangeAsync(newProducts);
await _unitOfWork.CommitAsync(userId: currentUserId);

foreach (var p in products) p.Price *= 1.1m;
_unitOfWork.Products.UpdateRange(products);
await _unitOfWork.CommitAsync(userId: currentUserId);
```

Call `UpdateRange` (or `Update`) even for entities you loaded through the same unit of work. Only change-tracking providers notice changes without it.

To record the user in the audit fields, see [Auditing](auditing.md#the-current-user).

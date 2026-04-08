# Advanced Usage

---

## Filtering, sorting, and eager loading

All query methods accept optional `condition`, `orderBy`, and `include` parameters:

```csharp
var products = await _unitOfWork.Products.GetAllAsync(
    condition: p => p.IsActive && p.Price > 10m,
    orderBy: q => q.OrderByDescending(p => p.Price).ThenBy(p => p.Name),
    include: q => q.Include(p => p.Category));
```

---

## Projecting to a DTO (selector)

Use a `selector` expression to project results without loading the full entity. This reduces data transfer and avoids materializing objects you don't need.

```csharp
// Project to a DTO
record ProductSummary(long Id, string Name, decimal Price);

var summaries = await _unitOfWork.Products.GetAllAsync(
    selector: p => new ProductSummary(p.Id, p.Name, p.Price),
    condition: p => p.IsActive,
    orderBy: q => q.OrderBy(p => p.Name));

// Project to a scalar
var names = await _unitOfWork.Products.GetAllAsync(
    selector: p => p.Name);
```

---

## Paged queries

Use `GetAllPagedAsync` to return a `Paged<T>` result suitable for list views and APIs:

```csharp
Paged<Product> page = await _unitOfWork.Products.GetAllPagedAsync(
    condition: p => p.IsActive,
    orderBy: q => q.OrderBy(p => p.Name),
    pageIndex: 2,
    pageSize: 25);

foreach (var item in page.Results)
{
    Console.WriteLine(item.Name);
}

Console.WriteLine($"Page {page.Page} of {page.TotalPages} ({page.TotalCount} total)");
Console.WriteLine($"Has next: {page.HasNext}, Has previous: {page.HasPrevious}");
```

---

## Disabling change tracking (read-only queries)

Set `track: false` for queries whose results will not be modified. This avoids the overhead of EF change tracking and improves performance for read-heavy paths:

```csharp
var products = await _unitOfWork.Products.GetAllAsync(
    condition: p => p.IsActive,
    track: false);
```

---

## Bypassing global query filters

Use `ignoreQueryFilters: true` to see records excluded by global EF query filters (e.g. soft-deleted entities):

```csharp
var allIncludingDeleted = await _unitOfWork.Products.GetAllAsync(
    ignoreQueryFilters: true);
```

---

## Split queries for large includes

When including multiple collection navigations, use `useSplitQueries: true` to avoid a Cartesian explosion. See the [EF Core documentation](https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries) for details.

```csharp
var products = await _unitOfWork.Products.GetAllAsync(
    include: q => q.Include(p => p.Tags).Include(p => p.Reviews),
    useSplitQueries: true);
```

---

## Bulk insert and update

```csharp
// Add multiple entities in one commit
var newProducts = new List<Product>
{
    new() { Name = "Widget A", Price = 9.99m },
    new() { Name = "Widget B", Price = 14.99m },
};

await _unitOfWork.Products.AddRangeAsync(newProducts);
await _unitOfWork.CommitAsync(userId: currentUserId);

// Update multiple entities
foreach (var p in products) p.Price *= 1.1m;
_unitOfWork.Products.UpdateRange(products);
await _unitOfWork.CommitAsync(userId: currentUserId);
```

---

## Committing with a user ID

Pass the authenticated user's ID so audit fields (`CreatedBy`, `ModifiedBy`) are populated automatically:

```csharp
await _unitOfWork.CommitAsync(userId: currentUserId);
```

If no `userId` is passed, `DefaultUserKey` (defined in your `UnitOfWork` implementation) is used instead.

---

## Custom repository methods

Encapsulate domain-specific queries inside the repository, reusing the built-in helpers:

```csharp
public interface IProductRepository : IRepository<Product, long>
{
    Task<IEnumerable<Product>> GetActiveByCategoryAsync(long categoryId);
    Task<bool> SkuExistsAsync(string sku);
}

public class ProductRepository : BaseRepository<Product, long>, IProductRepository
{
    public ProductRepository(DbContext context) : base(context) { }

    public async Task<IEnumerable<Product>> GetActiveByCategoryAsync(long categoryId)
        => await GetAllAsync(
            condition: p => p.IsActive && p.CategoryId == categoryId,
            orderBy: q => q.OrderBy(p => p.Name),
            track: false);

    public async Task<bool> SkuExistsAsync(string sku)
        => await AnyAsync(p => p.Sku == sku);
}

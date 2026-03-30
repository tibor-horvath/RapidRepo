# Repositories

---

## Choosing an interface

Pick the interface that matches the access pattern you need:

| Interface | Extends | Purpose |
|---|---|---|
| `IRepository<TEntity, TKey>` | `IReadOnlyRepository` + `IWriteRepository` | Full CRUD |
| `IReadOnlyRepository<TEntity, TKey>` | — | Queries only |
| `IWriteRepository<TEntity, TKey>` | — | Writes only |

```csharp
// Full CRUD
public interface IProductRepository : IRepository<Product, long>
{
    // Add custom methods here
}

// Read-only (e.g. for a reporting service)
public interface IProductReadRepository : IReadOnlyRepository<Product, long>
{
}

// Write-only (e.g. for an import pipeline)
public interface IProductWriteRepository : IWriteRepository<Product, long>
{
}
```

---

## Choosing a base class

Pick the implementation base class that matches your interface:

| Base class | Use with |
|---|---|
| `BaseRepository<TEntity, TKey>` | `IRepository<TEntity, TKey>` |
| `ReadOnlyRepository<TEntity, TKey>` | `IReadOnlyRepository<TEntity, TKey>` |
| `WriteRepository<TEntity, TKey>` | `IWriteRepository<TEntity, TKey>` |

---

## Implementation examples

### Full CRUD repository

```csharp
public class ProductRepository : BaseRepository<Product, long>, IProductRepository
{
    public ProductRepository(DbContext context) : base(context)
    {
    }
}
```

### Read-only repository

```csharp
public class ProductReadRepository : ReadOnlyRepository<Product, long>, IProductReadRepository
{
    public ProductReadRepository(DbContext context) : base(context)
    {
    }
}
```

### Write-only repository

```csharp
public class ProductWriteRepository : WriteRepository<Product, long>, IProductWriteRepository
{
    public ProductWriteRepository(DbContext context) : base(context)
    {
    }
}
```

---

## Adding custom methods

You can call any of the inherited `protected` query helpers directly from within your repository implementation, keeping the custom logic inside the data layer:

```csharp
public interface IProductRepository : IRepository<Product, long>
{
    Task<IEnumerable<Product>> GetByCategoryAsync(long categoryId);
    Task<bool> ExistsByNameAsync(string name);
}

public class ProductRepository : BaseRepository<Product, long>, IProductRepository
{
    public ProductRepository(DbContext context) : base(context) { }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(long categoryId)
        => await GetAllAsync(
            condition: p => p.CategoryId == categoryId,
            orderBy: q => q.OrderBy(p => p.Name));

    public async Task<bool> ExistsByNameAsync(string name)
        => await AnyAsync(p => p.Name == name);
}
```

---

## DI registration

Register each repository with a scoped lifetime, matching the `DbContext` scope:

```csharp
builder.Services.AddScoped<IProductRepository, ProductRepository>();
```

See [Unit of Work](unit-of-work.md) for how to wire everything together.

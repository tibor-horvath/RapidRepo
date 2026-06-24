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

| Base class | Use with | Notes |
|---|---|---|
| `BaseRepository<TEntity, TKey>` | user-defined `IRepository<,>` sub-interface | extend to add custom methods |
| `ReadOnlyRepository<TEntity, TKey>` | user-defined `IReadOnlyRepository<,>` sub-interface | extend to add custom methods |
| `WriteRepository<TEntity, TKey>` | user-defined `IWriteRepository<,>` sub-interface | extend to add custom methods |
| `Repository<TEntity, TKey>` | `IRepository<TEntity, TKey>` directly | no customisation needed; used by `RegisterGenericRepositories` |

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

## When no custom methods are needed

If a repository adds no methods beyond standard CRUD, you do not need to create a custom interface or class. Enable `RegisterGenericRepositories` in `AddRapidRepo` and inject the root interface directly:

```csharp
// Program.cs
builder.Services.AddRapidRepo(options =>
{
    options.RegisterGenericRepositories = true;
});

// Inject directly — no IUserRepository or UserRepository file needed
public class UserService(IRepository<User, Guid> users) { ... }
```

When you later need a custom method, create the interface and class as normal — the specific registration will take precedence automatically.

For manual DI registration without the extension package:

```csharp
builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));
```

---

## Adding custom methods

You can call any of the inherited query methods/helpers directly from within your repository implementation, keeping the custom logic inside the data layer:

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

### Convention-based (recommended)

Install `RapidRepo.Extensions.DependencyInjection` and use `AddRapidRepo` to auto-discover all repositories in one call:

```csharp
builder.Services.AddRapidRepo(options =>
{
    options.ScanAssembliesContaining<ProductRepository>();
    options.UseUnitOfWork<IAppUnitOfWork, AppUnitOfWork>();
});
```

See [Dependency Injection](../RapidRepo.Extensions.DependencyInjection/dependency-injection.md) for the full reference.

### Manual registration

Register each repository individually with a scoped lifetime, matching the `DbContext` scope:

```csharp
builder.Services.AddScoped<IProductRepository, ProductRepository>();
```

See [Unit of Work](unit-of-work.md) for how to wire everything together.

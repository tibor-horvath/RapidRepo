# Repositories

The repository contracts live in the core `RapidRepo` package and do not depend on any persistence technology. A provider package, such as [RapidRepo.EntityFrameworkCore](../RapidRepo.EntityFrameworkCore/entity-framework-core.md), implements them. Code written against the contracts works with any provider.

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

An interface derived from these contracts is **portable**: any provider can implement it. A provider may also offer its own extended interface, for example `IEfRepository<TEntity, TKey>`, which adds EF Core's `IQueryable`. Derive from that one only when the repository really needs the provider.

---

## Choosing a base class

Implementations come from the provider. For EF Core:

| Base class | Use with |
|---|---|
| `EfRepository<TEntity, TKey>` | a user-defined `IRepository<,>` sub-interface, or `IRepository<,>` directly |
| `EfReadOnlyRepository<TEntity, TKey>` | a user-defined `IReadOnlyRepository<,>` sub-interface |
| `EfWriteRepository<TEntity, TKey>` | a user-defined `IWriteRepository<,>` sub-interface |

```csharp
public class ProductRepository(AppDbContext context) : EfRepository<Product, long>(context), IProductRepository
{
}
```

See [EF Core repositories](../RapidRepo.EntityFrameworkCore/entity-framework-core.md#repositories) for the constructor options, custom query methods, and the 8.x names that still compile.

Providers build on two provider-neutral base classes in the core, `WriteRepositoryBase<TEntity, TKey>` and `UnitOfWorkBase<TUserKey>`. They hold the write and audit rules described below. See [Writing a provider](providers.md).

---

## How writes behave

These rules are part of the contract, so they hold for every provider:

1. **Writes are staged.** Nothing reaches the store until the unit of work commits.
2. **`Delete` soft-deletes an `IDeletableEntity`** and permanently removes any other entity.
3. **Audit fields are stamped at commit time**, with the commit's user and the unit of work's clock. See [Auditing](auditing.md#how-auditing-works).
4. **Generated keys are populated on the entity after the commit.**
5. **Changing a loaded entity without calling `Update` is not portable.** EF Core persists such a change because it tracks loaded entities; other providers do not. Always call `Update`.

---

## Soft delete, restore, and permanent removal

For an entity implementing `IDeletableEntity`, `Delete` marks it as deleted instead of removing the row, and reads exclude it by default. `Restore` / `RestoreById` bring it back, and `HardDelete` / `HardDeleteById` remove the row permanently. See [Soft Delete](soft-delete.md).

---

## When no custom methods are needed

If a repository adds no methods beyond standard CRUD, you do not need a custom interface or class. Enable `RegisterGenericRepositories` in `AddRapidRepo` and inject the root interface directly:

```csharp
builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<AppDbContext>();
    options.RegisterGenericRepositories = true;
});

// No IUserRepository or UserRepository file needed
public class UserService(IRepository<User, Guid> users) { ... }
```

When you later need a custom method, create the interface and class as normal. The specific registration takes precedence automatically.

---

## Adding custom methods

Call the inherited query methods from your repository implementation to keep custom logic inside the data layer:

```csharp
public interface IProductRepository : IRepository<Product, long>
{
    Task<IEnumerable<Product>> GetByCategoryAsync(long categoryId);
    Task<bool> ExistsByNameAsync(string name);
}

public class ProductRepository(AppDbContext context) : EfRepository<Product, long>(context), IProductRepository
{
    public Task<IEnumerable<Product>> GetByCategoryAsync(long categoryId)
        => GetAllAsync(
            condition: p => p.CategoryId == categoryId,
            orderBy: SortOrder<Product>.By(p => p.Name));

    public Task<bool> ExistsByNameAsync(string name)
        => AnyAsync(p => p.Name == name);
}
```

For queries the contract cannot express, the EF provider offers `Query()` and `BuildQuery()`. See [Custom queries](../RapidRepo.EntityFrameworkCore/entity-framework-core.md#custom-queries).

---

## DI registration

Install `RapidRepo.Extensions.DependencyInjection` (the EF Core package already depends on it) and let `AddRapidRepo` discover your repositories:

```csharp
builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<AppDbContext>();
    options.ScanAssembliesContaining<ProductRepository>();
    options.UseUnitOfWork<AppUnitOfWork>();
});
```

See [Dependency Injection](../RapidRepo.Extensions.DependencyInjection/dependency-injection.md) for the full reference. To register by hand instead, add each repository with a scoped lifetime:

```csharp
builder.Services.AddScoped<IProductRepository, ProductRepository>();
```

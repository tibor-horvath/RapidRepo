# RapidRepo.EntityFrameworkCore

The Entity Framework Core provider for [RapidRepo](https://www.nuget.org/packages/RapidRepo): repositories and a Unit of Work that implement the RapidRepo contracts on top of a `DbContext`, plus `UseEntityFrameworkCore<TContext>()` for dependency injection.

## Installation

```bash
dotnet add package RapidRepo.EntityFrameworkCore
```

This brings in `RapidRepo` (entities and contracts) and `RapidRepo.Extensions.DependencyInjection` (`AddRapidRepo`). Targets .NET 10 (EF Core 10) and .NET 11 (EF Core 11).

## Quick start

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.AddSoftDeleteQueryFilters();   // named filter "RapidRepo.SoftDelete"
}

public class ProductRepository(AppDbContext db) : EfRepository<Product, long>(db), IProductRepository;

public class AppUnitOfWork(AppDbContext db, IProductRepository products, IServiceProvider services)
    : EfUnitOfWork<Guid>(db, Guid.Empty, services), IAppUnitOfWork
{
    public IProductRepository Products { get; } = products;
}

builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<AppDbContext>();
    options.ScanAssembliesContaining<ProductRepository>();
    options.RegisterGenericRepositories = true;
    options.UseUnitOfWork<AppUnitOfWork>();
});
```

EF Core specific reads go through `EfReadOptions<TEntity>`:

```csharp
var products = await repo.GetAllAsync(
    p => p.IsActive,
    SortOrder<Product>.By(p => p.Name),
    new EfReadOptions<Product> { Include = q => q.Include(p => p.Category), Track = false });
```

## Types

- `EfRepository<TEntity, TId>`: a full repository over `DbContext.Set<TEntity>()`. Derive from it for custom repositories.
- `EfRepository<TEntity, TId, TContext>`: the same, bound to a specific `DbContext` type.
- `EfReadOnlyRepository<TEntity, TId>` / `EfWriteRepository<TEntity, TId>`: read-only and write-only bases.
- `EfUnitOfWork<TUserKey>`: stamps audit fields and saves the `DbContext`. It never disposes the context.
- `EfReadOptions<TEntity>`: includes, tracking, split queries, an `OrderBy` override, and bypassing all query filters.
- `IEfRepository<TEntity, TId>` / `IEfReadOnlyRepository<TEntity, TId>`: the contracts plus `Query()`, which returns an `IQueryable<TEntity>`.
- `ModelBuilder.AddSoftDeleteQueryFilters()`: excludes soft-deleted rows by default, as a named filter, so `ReadOptions.IncludeDeleted` bypasses soft delete only and leaves other named filters, such as a tenant filter, in force.
- `ModelBuilder.AddTenantQueryFilters(this)` and `ITenantDbContext<TTenantKey>`: isolate `ITenantEntity` rows per tenant with a named `RapidRepo.Tenant` filter. `TenancyValidator` checks the model at startup, on first use, or in a test.
- `ModelConfigurationBuilder.AddConcurrencyTokens()`: marks `IVersionedEntity.Version` as a concurrency token, for optimistic concurrency.
- `UseEntityFrameworkCore<TContext>()` / `UseEntityFrameworkCore()`: the provider for `AddRapidRepo`. It registers the `DbContext` forwarder and generic repositories per `DbSet`.

The 8.x names (`Repository<,>`, `BaseRepository<,>`, `ReadOnlyRepository<,>`, `WriteRepository<,>`, `UnitOfWork<>`) still compile but are obsolete (`RRMIG001`) and will be removed in 10.0.

## Documentation

- [EF Core guide](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo.EntityFrameworkCore/entity-framework-core.md)
- [EF Core registration](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo.EntityFrameworkCore/ef-core-registration.md)
- [Migrating from 8.x to 9.0](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/migration/8.x-to-9.0.md)
- [RapidRepo documentation](https://github.com/tibor-horvath/RapidRepo#documentation)

Source and issues: [github.com/tibor-horvath/RapidRepo](https://github.com/tibor-horvath/RapidRepo). Licensed under MIT.

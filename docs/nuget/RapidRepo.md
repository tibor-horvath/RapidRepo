# RapidRepo

A repository and Unit of Work implementation for .NET on top of Entity Framework Core: consistent CRUD, auditing, soft delete and paging without hand-writing the same data access code for every entity.

## Features

- `IRepository<TEntity, TId>` plus read-only and write-only variants
- Unit of Work with a single `CommitAsync()`
- Built-in auditing (`CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`)
- Soft delete (`DeletedAt`, `DeletedBy`) with restore and permanent delete
- Paged results via `Paged<T>`, filtering, projection and bulk operations
- Any non-null key type: `int`, `long`, `Guid`, `string`
- Targets .NET 8, 9 and 10 (EF Core 8, 9 and 10)

## Installation

```bash
dotnet add package RapidRepo
```

## Quick start

```csharp
// Entity
public class Product : BaseEntity<long>
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// Unit of Work
public interface IAppUnitOfWork : IUnitOfWork<Guid>, IDisposable
{
    IRepository<Product, long> Products { get; }
}

public class AppUnitOfWork : UnitOfWork<Guid>, IAppUnitOfWork
{
    public IRepository<Product, long> Products { get; }

    public AppUnitOfWork(AppDbContext dbContext, IRepository<Product, long> products)
        : base(dbContext, Guid.Empty)
    {
        Products = products;
    }
}

// Usage
public class ProductService(IAppUnitOfWork unitOfWork)
{
    public async Task CreateAsync(Product product)
    {
        await unitOfWork.Products.AddAsync(product);
        await unitOfWork.CommitAsync();
    }
}
```

Register `Repository<Product, long>` and `AppUnitOfWork` with `AddScoped<>`, or let `AddRapidRepo(...)` from the DI package do it for you.

## Companion packages

| Package | What it adds |
|---|---|
| [RapidRepo.Extensions.DependencyInjection](https://www.nuget.org/packages/RapidRepo.Extensions.DependencyInjection) | `AddRapidRepo(...)`: scans assemblies and registers repositories and the Unit of Work |
| [RapidRepo.SourceGenerators](https://www.nuget.org/packages/RapidRepo.SourceGenerators) | `[GenerateUnitOfWork]`: generates the repository properties of your Unit of Work |

## Documentation

- [Entities](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo/entities.md)
- [Repositories](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo/repositories.md)
- [Unit of Work](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo/unit-of-work.md)
- [API reference](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo/api-reference.md)
- [Advanced usage](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo/advanced.md)

Source, issues and changelog: [github.com/tibor-horvath/RapidRepo](https://github.com/tibor-horvath/RapidRepo). Licensed under MIT.

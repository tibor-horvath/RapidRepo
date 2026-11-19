# RapidRepo

The core of RapidRepo: entities, repository and Unit of Work contracts, and the audit and soft-delete rules, independent of any persistence technology. Add a provider package to use it with a database. [RapidRepo.EntityFrameworkCore](https://www.nuget.org/packages/RapidRepo.EntityFrameworkCore) is the provider available today.

## Why RapidRepo?

It gives you the data-layer plumbing most applications rebuild by hand, with consistent rules. When the Unit of Work commits, it stamps audit fields with the committing user and a clock you can replace in tests. Entities you mark as deletable are soft-deleted instead of removed and, once the provider's filter is set up, hidden from reads, with explicit restore and permanent delete. Paging and sorting are declarative. Domain and application projects can reference only this dependency-free package, while the provider (EF Core today) stays in the infrastructure project, where its own features are still available when you need them. A provider-neutral conformance suite in the repository defines the contract, so other providers can implement the same interfaces with the same behaviour.

## Features

- `IRepository<TEntity, TId>` plus read-only and write-only variants
- Unit of Work with a single `CommitAsync()`
- Base entities with built-in auditing (`CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`)
- Soft delete (`DeletedAt`, `DeletedBy`) with restore and permanent delete
- Paged results via `Paged<T>`, declarative sorting with `SortOrder<T>`, projections
- Any non-null key type: `int`, `long`, `Guid`, `string`
- No package dependencies; targets .NET 10 and 11

## Installation

Most applications install a provider, which brings this package in:

```bash
dotnet add package RapidRepo.EntityFrameworkCore
```

Reference `RapidRepo` on its own in projects that should only see the contracts, such as a domain or application layer:

```bash
dotnet add package RapidRepo
```

## Example

```csharp
public class Product : BaseAuditableDeletableEntity<long, Guid>
{
    public string Name { get; set; } = string.Empty;
}

public interface IProductRepository : IRepository<Product, long> { }

public class CatalogService(IProductRepository products, IUnitOfWork<Guid> unitOfWork)
{
    public Task<Paged<Product>> PageAsync(int page)
        => products.GetAllPagedAsync(orderBy: SortOrder<Product>.By(p => p.Name), pageIndex: page, pageSize: 20);

    public async Task RemoveAsync(long id, Guid user)
    {
        products.DeleteById(id);             // soft delete
        await unitOfWork.CommitAsync(user);  // stamps DeletedAt and DeletedBy
    }
}
```

## Related packages

| Package | What it adds |
|---|---|
| [RapidRepo.EntityFrameworkCore](https://www.nuget.org/packages/RapidRepo.EntityFrameworkCore) | The EF Core provider |
| [RapidRepo.Extensions.DependencyInjection](https://www.nuget.org/packages/RapidRepo.Extensions.DependencyInjection) | `AddRapidRepo(...)`: scans assemblies and registers repositories and the Unit of Work |
| [RapidRepo.SourceGenerators](https://www.nuget.org/packages/RapidRepo.SourceGenerators) | `[GenerateUnitOfWork]`: generates the repository properties of your Unit of Work |

## Documentation

- [Entities](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo/entities.md)
- [Repositories](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo/repositories.md)
- [Unit of Work](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo/unit-of-work.md)
- [API reference](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo/api-reference.md)
- [Advanced usage](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo/advanced.md)
- [Writing a provider](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/RapidRepo/providers.md)
- [Migrating from 8.x to 9.0](https://github.com/tibor-horvath/RapidRepo/blob/master/docs/migration/8.x-to-9.0.md)

Source, issues and changelog: [github.com/tibor-horvath/RapidRepo](https://github.com/tibor-horvath/RapidRepo). Licensed under MIT.

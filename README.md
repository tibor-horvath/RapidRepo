<h1>
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/social-preview-dark.png">
    <img alt="RapidRepo — Repository & Unit of Work for .NET" src="assets/social-preview-light.png" width="100%">
  </picture>
</h1>

[![Build](https://github.com/tibor-horvath/RapidRepo/actions/workflows/dotnet-build.yml/badge.svg)](https://github.com/tibor-horvath/RapidRepo/actions/workflows/dotnet-build.yml)
[![Release](https://github.com/tibor-horvath/RapidRepo/actions/workflows/dotnet-release.yml/badge.svg)](https://github.com/tibor-horvath/RapidRepo/actions/workflows/dotnet-release.yml)
[![NuGet](https://img.shields.io/nuget/v/RapidRepo.svg?logo=nuget)](https://www.nuget.org/packages/RapidRepo)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Built by Tibor Horváth](https://tiborhorvath.dev/badge/built-by-compact.svg)](https://tiborhorvath.dev)

RapidRepo is a repository and Unit of Work implementation for .NET applications. It gives you a clean, consistent data access layer for common CRUD operations, with auditing, soft delete and paging built in. The contracts are provider-neutral; Entity Framework Core is the provider available today.

## At a glance

```csharp
// Filter, sort and page in one call
Paged<Product> page = await uow.Products.GetAllPagedAsync(
    p => p.Price > 10, SortOrder<Product>.By(p => p.Name), pageIndex: 1, pageSize: 20);

// Soft delete: sets DeletedAt instead of removing the row; reads skip it from now on
uow.Products.Delete(product);

// One commit stamps CreatedBy/ModifiedAt/DeletedBy… with the user and a single timestamp
await uow.CommitAsync(currentUserId);

// Several commits that succeed or fail together, safe with EF Core's EnableRetryOnFailure
await uow.ExecuteInTransactionAsync(async ct =>
{
    await uow.Orders.AddAsync(order, ct);
    await uow.CommitAsync(cancellationToken: ct);     // order.Id is now set
    await uow.Invoices.AddAsync(new Invoice { OrderId = order.Id }, ct);
    await uow.CommitAsync(cancellationToken: ct);
});
```

The [Quick Start](#quick-start) shows the setup behind `uow`.

## Why RapidRepo?

Most data layers end up rebuilding the same plumbing: setting `CreatedBy` and `ModifiedAt` before every save, a soft-delete flag that every query must remember to filter, paging and sorting helpers, and the DI registrations that wire it all together. RapidRepo provides that plumbing once, with one set of rules:

- **Auditing in one place.** `CommitAsync` stamps the created and modified fields of auditable entities (`IAuditableEntity`, `BaseAuditableEntity`) and the deleted fields of soft-deletable ones with the committing user (or the Unit of Work's default user) and one timestamp per commit. The time comes from a `TimeProvider` that tests can replace. Services don't set these fields by hand, as long as they save through the Unit of Work and not `DbContext.SaveChanges()`.
- **Soft delete you opt into per entity.** For entities that implement `IDeletableEntity` (or derive from `BaseAuditableDeletableEntity`), `Delete` sets `DeletedAt` instead of removing the row. `Restore` and `HardDelete` are explicit operations. Other entities are deleted normally. Reads exclude deleted rows unless you pass `IncludeDeleted`; with EF Core, you turn this on once with `modelBuilder.AddSoftDeleteQueryFilters()`.
- **Transactions that survive retries.** `ExecuteInTransactionAsync` makes several commits succeed or fail together. It runs inside EF Core's execution strategy, so it works with `EnableRetryOnFailure`, where a hand-opened transaction would throw. Nested calls join the outer transaction, so services that each use one can call each other. See [Transactions](docs/RapidRepo/transactions.md).
- **Lost updates caught, not silently overwritten.** Entities that implement `IVersionedEntity` get a version that is checked and incremented at commit. A stale update throws a provider-neutral `ConcurrencyException`, which you can map to HTTP 409 without catching EF Core types. See [Optimistic Concurrency](docs/RapidRepo/concurrency.md).
- **Your domain can stay free of EF Core.** Entities, repository interfaces and the Unit of Work interface only need the `RapidRepo` contracts package, which has no dependencies. In a layered solution, only the infrastructure project that installs `RapidRepo.EntityFrameworkCore` references EF Core, and the interfaces your services use are easy to mock.
- **A specified contract, not just an implementation.** Write, soft-delete, audit and query semantics are pinned down by a provider-neutral xUnit conformance suite in this repository. The EF Core provider runs it, and a new provider can run it to prove it behaves the same way behind the same interfaces. See [Writing a provider](docs/RapidRepo/providers.md).
- **EF Core is still there for hard queries.** When the contract isn't enough, `EfReadOptions` adds includes, tracking and split queries, `Query()` returns an `IQueryable`, and the `DbContext` is still yours.
- **Little boilerplate.** With `RegisterGenericRepositories = true`, you can inject `IRepository<TEntity, TKey>` without writing a class. `AddRapidRepo` scans assemblies and registers your repositories and Unit of Work. `[GenerateUnitOfWork]` generates the Unit of Work's repository properties and its interface.

### "Isn't `DbContext` already a repository and Unit of Work?"

It is, and RapidRepo builds on it rather than hiding it. What it adds is a smaller, intention-revealing surface; audit and soft-delete rules enforced in one place instead of in every service; and contracts that keep EF Core types out of the layers that don't need them.

**When it may not fit:** small apps where using `DbContext` directly is simpler, or data access that is mostly complex, store-specific queries. Those would live behind `Query()` or the `DbContext` anyway, so the abstraction would add little.

## Features

- Repository and Unit of Work abstractions that do not depend on the persistence technology
- EF Core provider, with includes, tracking, split queries and `IQueryable` when you need them
- Consistent and reusable CRUD patterns
- Supports non-null entity key types (e.g. `int`, `Guid`, `string`)
- Built-in auditing (`CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`), stamped at commit with a controllable clock
- Built-in soft delete (`DeletedAt`, `DeletedBy`) with restore and permanent-delete escape hatches
- Explicit transactions across several commits (`ExecuteInTransactionAsync`), safe with EF Core's `EnableRetryOnFailure`
- Opt-in optimistic concurrency (`IVersionedEntity`) with a provider-neutral `ConcurrencyException`
- Opt-in row-level multi-tenancy (`ITenantEntity<TTenantKey>`): tenant stamping, filtering, and write guards that check the stored row, so a request can't update or delete another tenant's data through the repositories and Unit of Work. Raw SQL and other paths around EF Core are not covered; see [what is not guarded](docs/RapidRepo/multi-tenancy.md#what-is-not-guarded)
- Paged query results via `Paged<T>`, with declarative sorting
- Optional companion packages for DI registration and Unit of Work source generation

## Requirements

- .NET 10 or .NET 11
- For the EF Core provider: EF Core 10 or 11

## Packages

| Package | Version | Downloads | What it contains |
|---|---|---|---|
| `RapidRepo` | [![NuGet](https://img.shields.io/nuget/v/RapidRepo.svg)](https://www.nuget.org/packages/RapidRepo) | [![Downloads](https://img.shields.io/nuget/dt/RapidRepo.svg)](https://www.nuget.org/packages/RapidRepo) | Entities, repository and Unit of Work contracts, `ReadOptions`, `SortOrder<T>`, audit, soft-delete, concurrency and tenancy rules. No dependencies |
| [`RapidRepo.EntityFrameworkCore`](docs/RapidRepo.EntityFrameworkCore/entity-framework-core.md) | [![NuGet](https://img.shields.io/nuget/v/RapidRepo.EntityFrameworkCore.svg)](https://www.nuget.org/packages/RapidRepo.EntityFrameworkCore) | [![Downloads](https://img.shields.io/nuget/dt/RapidRepo.EntityFrameworkCore.svg)](https://www.nuget.org/packages/RapidRepo.EntityFrameworkCore) | The EF Core provider: `EfRepository`, `EfUnitOfWork`, `EfReadOptions`, `AddSoftDeleteQueryFilters()`, `AddTenantQueryFilters()`, `AddConcurrencyTokens()`, `UseEntityFrameworkCore<TContext>()` |
| [`RapidRepo.Extensions.DependencyInjection`](docs/RapidRepo.Extensions.DependencyInjection/dependency-injection.md) | [![NuGet](https://img.shields.io/nuget/v/RapidRepo.Extensions.DependencyInjection.svg)](https://www.nuget.org/packages/RapidRepo.Extensions.DependencyInjection) | [![Downloads](https://img.shields.io/nuget/dt/RapidRepo.Extensions.DependencyInjection.svg)](https://www.nuget.org/packages/RapidRepo.Extensions.DependencyInjection) | `AddRapidRepo(...)`: scans assemblies and registers repositories and the Unit of Work |
| [`RapidRepo.SourceGenerators`](docs/RapidRepo.SourceGenerators/generate-unit-of-work.md) | [![NuGet](https://img.shields.io/nuget/v/RapidRepo.SourceGenerators.svg)](https://www.nuget.org/packages/RapidRepo.SourceGenerators) | [![Downloads](https://img.shields.io/nuget/dt/RapidRepo.SourceGenerators.svg)](https://www.nuget.org/packages/RapidRepo.SourceGenerators) | `[GenerateUnitOfWork]`: emits repository properties and a companion interface on your `partial` Unit of Work class |

For an EF Core application, one package brings in the core and the DI extension:

```bash
dotnet add package RapidRepo.EntityFrameworkCore
```

The source generator is optional. Reference it as an analyzer:

```xml
<PackageReference Include="RapidRepo.SourceGenerators" Version="..."
    PrivateAssets="all"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false" />
```

Upgrading from 8.x? See the [migration guide](docs/migration/8.x-to-9.0.md).

## Quick Start

### Minimal setup

If standard CRUD is all you need, you don't need repository classes or assembly scanning. With [`RapidRepo.SourceGenerators`](docs/RapidRepo.SourceGenerators/generate-unit-of-work.md) referenced (see [Packages](#packages)), `[GenerateUnitOfWork]` reads the `DbSet`s of your context and generates a repository property for each one, plus an interface that declares them:

```csharp
using Microsoft.EntityFrameworkCore;
using RapidRepo.Attributes;            // [GenerateUnitOfWork]
using RapidRepo.Entities;              // base entity classes
using RapidRepo.EntityFrameworkCore;   // EfUnitOfWork, EfRepository
using RapidRepo.Extensions;            // AddSoftDeleteQueryFilters()
using RapidRepo.UnitOfWork;            // IUnitOfWork<TUserKey>

namespace MyApp.Data;

// 1. Define an entity
public class Product : BaseAuditableDeletableEntity<long, Guid>
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// 2. Map it, with soft-deleted rows filtered out by default
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.AddSoftDeleteQueryFilters();
}

// 3. Declare the Unit of Work; the generator adds a Products property and IAppUnitOfWorkRepositories.
//    Passing IServiceProvider lets it pick up the current-user settings from AddRapidRepo (see Auditing).
[GenerateUnitOfWork(typeof(AppDbContext))]
public partial class AppUnitOfWork(AppDbContext dbContext, IServiceProvider services)
    : EfUnitOfWork<Guid>(dbContext, Guid.Empty, services), IAppUnitOfWork;

public interface IAppUnitOfWork : IUnitOfWork<Guid>, IAppUnitOfWorkRepositories;

// 4. Register in DI (Program.cs): nothing is scanned
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<AppDbContext>();
    options.UseUnitOfWork<AppUnitOfWork>();        // registered as IAppUnitOfWork and IUnitOfWork<Guid>
});

// 5. Use in a service: repositories are reached through the Unit of Work
public class ProductService(IAppUnitOfWork unitOfWork)
{
    public async Task CreateAsync(Product product, Guid currentUser)
    {
        await unitOfWork.Products.AddAsync(product);
        await unitOfWork.CommitAsync(currentUser);
    }
}
```

Without the generator, write the property yourself: `public IRepository<Product, long> Products => GetRepository<Product, long>();`, and declare it on `IAppUnitOfWork`. When you add a custom repository interface for an entity, the generated property uses it instead; scan the assembly that contains the repository so the `IServiceProvider` can resolve it.

> **Keep the `IServiceProvider`.** A unit of work built without one still works, but it cannot see `UseCurrentUserProvider`, `UseCurrentUser` or `RequireCurrentUser` from `AddRapidRepo`. Those settings are then ignored without an error, and commits without a user stamp `Guid.Empty`. See [Auditing — the current user](docs/RapidRepo/auditing.md#the-current-user).

### Custom repositories and a typed Unit of Work

When you need query methods beyond standard CRUD, write a repository for that entity and let `AddRapidRepo` find it by scanning. The entity and `AppDbContext` are the same as in the minimal setup; only the repository and the registration are new. With the generator, the `Products` property now uses `IProductRepository`:

```csharp
using RapidRepo.Repositories;              // SortOrder<T>
using RapidRepo.Repositories.Interfaces;   // IRepository<TEntity, TKey>

// 1. Custom repository: only needed for methods beyond standard CRUD
public interface IProductRepository : IRepository<Product, long>
{
    Task<IList<Product>> GetCheapestAsync(int count);
}

public class ProductRepository(AppDbContext context) : EfRepository<Product, long>(context), IProductRepository
{
    public async Task<IList<Product>> GetCheapestAsync(int count)
        => (await GetAllPagedAsync(orderBy: SortOrder<Product>.By(p => p.Price), pageSize: count)).Results;
}

// 2. Register in DI (Program.cs): scan the assembly that contains the repository
builder.Services.AddRapidRepo(options =>
{
    options.UseEntityFrameworkCore<AppDbContext>();
    options.ScanAssembliesContaining<ProductRepository>();
    options.UseUnitOfWork<AppUnitOfWork>();
});

// 3. Use it through the Unit of Work
var cheapest = await unitOfWork.Products.GetCheapestAsync(5);
```

<details>
<summary>Without the source generator: write the Unit of Work by hand</summary>

```csharp
public interface IAppUnitOfWork : IUnitOfWork<Guid>
{
    IProductRepository Products { get; }
}

public class AppUnitOfWork(AppDbContext dbContext, IProductRepository products, IServiceProvider services)
    : EfUnitOfWork<Guid>(dbContext, Guid.Empty, services), IAppUnitOfWork
{
    public IProductRepository Products { get; } = products;
}
```

`UseUnitOfWork<AppUnitOfWork>()` detects `IAppUnitOfWork` and registers the class under it. Entities without a property can still use the generic repository through `GetRepository<TEntity, TKey>()`.

</details>

## Documentation

Start at the [documentation index](docs/README.md).

The documentation is written with AI assistance and reviewed by the maintainer. Behaviour is pinned down by the conformance test suite; if anything in the docs doesn't match, please [open an issue](https://github.com/tibor-horvath/RapidRepo/issues).

- **Core concepts:** [Entities](docs/RapidRepo/entities.md) · [Repositories](docs/RapidRepo/repositories.md) · [Unit of Work](docs/RapidRepo/unit-of-work.md) · [Querying](docs/RapidRepo/querying.md)
- **Features:** [Soft Delete](docs/RapidRepo/soft-delete.md) · [Auditing](docs/RapidRepo/auditing.md) · [Optimistic Concurrency](docs/RapidRepo/concurrency.md) · [Transactions](docs/RapidRepo/transactions.md) · [Multi-Tenancy](docs/RapidRepo/multi-tenancy.md)
- **Reference:** [API Reference](docs/RapidRepo/api-reference.md) · [Writing a provider](docs/RapidRepo/providers.md)
- **Packages:** [EF Core](docs/RapidRepo.EntityFrameworkCore/entity-framework-core.md) · [EF Core registration](docs/RapidRepo.EntityFrameworkCore/ef-core-registration.md) · [Dependency Injection](docs/RapidRepo.Extensions.DependencyInjection/dependency-injection.md) · [Generated Unit of Work](docs/RapidRepo.SourceGenerators/generate-unit-of-work.md)
- **Upgrading:** [Migrating from 8.x to 9.0](docs/migration/8.x-to-9.0.md)

## Contributing

Issues and pull requests are welcome; see [CONTRIBUTING.md](CONTRIBUTING.md). Behaviour changes come with a test in the conformance suite, so every provider is held to them. Releases follow [Semantic Versioning](https://semver.org/), and the [changelog](CHANGELOG.md) lists every change. To report a security issue, see [SECURITY.md](SECURITY.md).

## License

RapidRepo is licensed under the MIT License. See [LICENSE](./LICENSE) for details.

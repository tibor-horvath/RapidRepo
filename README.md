# RapidRepo

[![Build](https://github.com/tibor-horvath/RapidRepo/actions/workflows/dotnet-build.yml/badge.svg)](https://github.com/tibor-horvath/RapidRepo/actions/workflows/dotnet-build.yml)
[![Release](https://github.com/tibor-horvath/RapidRepo/actions/workflows/dotnet-release.yml/badge.svg)](https://github.com/tibor-horvath/RapidRepo/actions/workflows/dotnet-release.yml)

RapidRepo is a repository pattern implementation for .NET applications. It uses Entity Framework Core to provide a clean and consistent data access layer for common CRUD operations.

## Features

- Repository and Unit of Work abstractions
- EF Core-based data access
- Consistent and reusable CRUD patterns
- Separation of business logic and persistence logic
- Supports non-null entity key types (e.g. `int`, `Guid`, `string`)
- Built-in auditing (`CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy`)
- Built-in soft delete support (`DeletedAt`, `DeletedBy`)
- Paged query results via `Paged<T>`
- Optional companion packages for DI registration and Unit of Work source generation

## Requirements

- .NET 10 SDK
- Entity Framework Core-compatible `DbContext`

## Installation

```bash
dotnet add package RapidRepo
```

## Companion packages

RapidRepo is intentionally split into a small core library and two optional packages:

| Package | What it adds |
|---|---|
| [`RapidRepo.Extensions.DependencyInjection`](docs/RapidRepo.Extensions.DependencyInjection/dependency-injection.md) | `AddRapidRepo(...)` — scans assemblies and registers repositories, open-generic fallbacks, and the unit of work |
| [`RapidRepo.SourceGenerators`](docs/RapidRepo.SourceGenerators/unit-of-work.md) | `[GenerateUnitOfWork]` — emits repository properties and a companion interface on your `partial` Unit of Work class |

```bash
dotnet add package RapidRepo.Extensions.DependencyInjection
dotnet add package RapidRepo.SourceGenerators
```

`RapidRepo.Extensions.DependencyInjection` declares a dependency on `RapidRepo` — installing it is enough; you do not need to add `RapidRepo` separately.

`RapidRepo.SourceGenerators` does not pull in `RapidRepo` automatically, but it requires it at compile time (`[GenerateUnitOfWork]`, `UnitOfWork<T>`, and the types the generator emits all live in the core package). Add both when using the generator:

```bash
dotnet add package RapidRepo
dotnet add package RapidRepo.SourceGenerators
```

The source generator must be referenced as an analyzer (not a regular assembly):

```xml
<PackageReference Include="RapidRepo.SourceGenerators" Version="..."
    PrivateAssets="all"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false" />
```

The companion packages are optional — you can use `RapidRepo` alone with manual `AddScoped<>` registration and hand-written Unit of Work properties. When you do adopt them, both require the core library; only the DI package installs it for you via NuGet.

## Quick Start

```csharp
// 1. Define an entity
public class Product : BaseEntity<long>
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// 2a. Zero-boilerplate — inject the root interface directly (no custom class needed)
//     Requires RegisterGenericRepositories = true in AddRapidRepo
public class CatalogService(IRepository<Product, long> products) { ... }

// 2b. Custom repository — only needed when adding methods beyond standard CRUD
public interface IProductRepository : IRepository<Product, long>
{
    Task<IEnumerable<Product>> GetByCategoryAsync(long categoryId);
}

public class ProductRepository : BaseRepository<Product, long>, IProductRepository
{
    public ProductRepository(DbContext context) : base(context) { }
    public async Task<IEnumerable<Product>> GetByCategoryAsync(long categoryId) => ...;
}

// 3. Create a Unit of Work
public interface IAppUnitOfWork : IUnitOfWork<Guid>, IDisposable
{
    IProductRepository Products { get; }
}

public class AppUnitOfWork : UnitOfWork<Guid>, IAppUnitOfWork
{
    public IProductRepository Products { get; }

    public AppUnitOfWork(AppDbContext dbContext, IProductRepository productRepository)
        : base(dbContext, Guid.Empty)
    {
        Products = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }
}

// 4. Register in DI (Program.cs)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Option A — zero-boilerplate (path 2a): enable open-generic fallback
builder.Services.AddRapidRepo(options =>
{
    options.RegisterGenericRepositories = true;
    options.UseUnitOfWork<AppUnitOfWork>(); // interface auto-detected
});

// Option B — custom repositories (path 2b): scan assemblies for IProductRepository etc.
builder.Services.AddRapidRepo(options =>
{
    options.ScanAssembliesContaining<ProductRepository>();
    options.RegisterGenericRepositories = true; // also allow direct IRepository<,> injection
    options.UseUnitOfWork<AppUnitOfWork>(); // interface auto-detected
});

// Option C — manual registration (no extra package required)
// builder.Services.AddScoped<IProductRepository, ProductRepository>();
// builder.Services.AddScoped<IAppUnitOfWork, AppUnitOfWork>();

// 5. Use in a service
public class ProductService
{
    private readonly IAppUnitOfWork _unitOfWork;
    public ProductService(IAppUnitOfWork unitOfWork) => _unitOfWork = unitOfWork;

    public async Task<IEnumerable<Product>> GetAllAsync()
        => await _unitOfWork.Products.GetAllAsync();

    public async Task CreateAsync(Product product)
    {
        await _unitOfWork.Products.AddAsync(product);
        await _unitOfWork.CommitAsync();
    }
}
```

## Documentation

| Package | Topic | Description |
|---|---|---|
| RapidRepo | [Entities](docs/RapidRepo/entities.md) | `BaseEntity`, `BaseAuditableEntity`, `BaseAuditableDeletableEntity`, soft delete |
| RapidRepo | [Repositories](docs/RapidRepo/repositories.md) | Interfaces, base classes, custom methods |
| RapidRepo | [Unit of Work](docs/RapidRepo/unit-of-work.md) | Setup, committing, auditing |
| RapidRepo | [API Reference](docs/RapidRepo/api-reference.md) | All read and write methods with parameters |
| RapidRepo | [Advanced Usage](docs/RapidRepo/advanced.md) | Filtering, projection, paging, bulk operations |
| RapidRepo.Extensions.DependencyInjection | [Dependency Injection](docs/RapidRepo.Extensions.DependencyInjection/dependency-injection.md) | `AddRapidRepo` extension, scanning options, filters, lifetime |
| RapidRepo.SourceGenerators | [Unit of Work](docs/RapidRepo.SourceGenerators/unit-of-work.md) | `[GenerateUnitOfWork]` source generator |

## License

RapidRepo is licensed under the MIT License. See [LICENSE](./LICENSE) for details.

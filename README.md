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

## Requirements

- .NET 10 SDK
- Entity Framework Core-compatible `DbContext`

## Installation

```bash
dotnet add package RapidRepo
```

## Quick Start

```csharp
// 1. Define an entity
public class Product : BaseEntity<long>
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// 2. Create a repository interface and implementation
public interface IProductRepository : IRepository<Product, long> { }

public class ProductRepository : BaseRepository<Product, long>, IProductRepository
{
    public ProductRepository(DbContext context) : base(context) { }
}

// 3. Create a Unit of Work
public interface IAppUnitOfWork : IUnitOfWork<Guid>, IDisposable
{
    IProductRepository Products { get; }
}

public class AppUnitOfWork : UnitOfWork<Guid>, IAppUnitOfWork
{
    public IProductRepository Products { get; }
    public override Guid DefaultUserKey => Guid.Empty;

    public AppUnitOfWork(AppDbContext dbContext, IProductRepository productRepository)
        : base(dbContext)
    {
        Products = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }
}

// 4. Register in DI (Program.cs)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IAppUnitOfWork, AppUnitOfWork>();

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

| Topic | Description |
|---|---|
| [Entities](docs/entities.md) | `BaseEntity`, `BaseAuditableEntity`, soft delete |
| [Repositories](docs/repositories.md) | Interfaces, base classes, custom methods, DI registration |
| [Unit of Work](docs/unit-of-work.md) | Setup, committing, auditing |
| [API Reference](docs/api-reference.md) | All read and write methods with parameters |
| [Advanced Usage](docs/advanced.md) | Filtering, projection, paging, bulk operations |

## License

RapidRepo is licensed under the MIT License. See [LICENSE](./LICENSE) for details.

# RapidRepo

[![Build](https://github.com/tibor-horvath/RapidRepo/actions/workflows/dotnet-build.yml/badge.svg)](https://github.com/tibor-horvath/RapidRepo/actions/workflows/dotnet-build.yml)
[![Release](https://github.com/tibor-horvath/RapidRepo/actions/workflows/dotnet-release.yml/badge.svg)](https://github.com/tibor-horvath/RapidRepo/actions/workflows/dotnet-release.yml)

## Documentation

RapidRepo is a comprehensive repository pattern implementation designed to streamline data access and manipulation in .NET applications. It leverages Entity Framework Core to provide a robust and flexible data access layer, ensuring that common data operations are handled efficiently and consistently.

### Purpose

The purpose of RapidRepo is to simplify data access and manipulation in .NET applications by providing a repository pattern implementation. It aims to offer a robust and flexible data access layer using Entity Framework Core.

### Features

- **Streamlined data access and manipulation**: Provides methods and abstractions that simplify common data operations in .NET applications.
- **Repository pattern implementation**: Separates data access logic from business logic, offering a structured way to perform CRUD (Create, Read, Update, Delete) operations.
- **Leveraging Entity Framework Core**: Uses a popular ORM framework to map database tables to .NET objects, simplifying database operations with object-oriented programming techniques.
- **Efficient and consistent data operations**: Optimizes queries and data retrieval to minimize latency and improve performance, ensuring atomic and reliable data manipulation.

### Usage

To use RapidRepo in your .NET application:

1. Install the RapidRepo NuGet package.
2. Configure the database connection string in your application's configuration file.
3. Create a repository class that inherits from the `BaseRepository` class provided by RapidRepo.
4. Implement any additional custom repository methods as needed.
5. Use the repository methods to perform data access and manipulation operations in your application.

### Example
```csharp
public class Product : BaseEntity<long>
{
    public string Name { get; set; }
    public string Description { get; set; }
}

public interface IProductRepository : IRepository<Product, long>
{
   // Add custom methods here
}   

public class ProductRepository : BaseRepository<Product, long>, IProductRepository
{ 
    public ProductRepository(DbContext context) 
    : base(context) 
    {         
    }
    // Add custom methods here
}

public interface ITestUnitOfWork : IUnitOfWork<Guid>, IDisposable
{
    IProductRepository Products { get; }
}

public class TestUnitOfWork : UnitOfWork<Guid>, IITestUnitOfWork
{
    public IProductRepository Products { get; }

    public override Guid DefaultUserId => default;

    public UnitOfWork(
        AppDbContext dbContext,
        IProductRepository productRepository)
        : base(dbContext)
    {
        Products = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    }
}

// Usage in a service public class ProductService
public class ProductService : IProductService
{ 
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<Product>> GetAllProductsAsync()
    {
        return await _unitOfWork.Products.GetAllAsync();
    }
}
```

### License

RapidRepo is licensed under the MIT License. See the [LICENSE](./LICENSE) file for more information.

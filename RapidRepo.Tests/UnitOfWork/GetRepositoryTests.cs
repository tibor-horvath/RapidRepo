using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.Tests.Repositories.TestData;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Tests.UnitOfWork;

public class GetRepositoryTests
{
    private readonly FactoryUnitOfWork _sut;
    private readonly TestDbContext _dbContext;

    public GetRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDatabase-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        _dbContext = new TestDbContext(options);
        _dbContext.Database.EnsureCreated();

        _sut = new FactoryUnitOfWork(_dbContext);
    }

    [Fact]
    public async Task GetRepository_WhenEntityAdded_ShouldPersistWithAuditFields()
    {
        // Arrange
        var utcBefore = DateTime.UtcNow;
        var userId = Guid.NewGuid();
        var employee = new Employee { FirstName = "Alice", LastName = "Smith" };

        // Act
        await _sut.Employees.AddAsync(employee);
        await _sut.CommitAsync(userId);

        // Assert
        var saved = _dbContext.Employees.FirstOrDefault();
        Assert.NotNull(saved);
        Assert.Equal(userId, saved.CreatedBy);
        Assert.True(saved.CreatedAt >= utcBefore && saved.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task GetRepository_WhenQueried_ReturnsPersistedEntities()
    {
        // Arrange
        var employee = new Employee { FirstName = "Bob", LastName = "Jones" };
        await _sut.Employees.AddAsync(employee);
        await _sut.CommitAsync();

        // Act — call GetRepository again to get a fresh instance backed by the same DbContext
        var result = _sut.Employees.Any(e => e.FirstName == "Bob");

        // Assert
        Assert.True(result);
    }

    // ── ResolveRepository tests ───────────────────────────────────────────────

    [Fact]
    public void ResolveRepository_WithServiceProvider_ReturnsRegisteredInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase("resolve-test"));
        services.AddScoped<IRepository<Employee, int>>(sp =>
            new RapidRepo.Repositories.Repository<Employee, int>(sp.GetRequiredService<TestDbContext>()));
        var sp = services.BuildServiceProvider();

        using var scope = sp.CreateScope();
        var db  = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        var uow = new DiUnitOfWork(db, scope.ServiceProvider);

        // Act
        var repo = uow.GetEmployees();

        // Assert
        Assert.NotNull(repo);
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new DiUnitOfWork(_dbContext, null!));
        Assert.Equal("serviceProvider", ex.ParamName);
    }

    [Fact]
    public void ResolveRepository_WithoutServiceProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        var uow = new FactoryUnitOfWork(_dbContext);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => uow.TryResolve());
        Assert.Contains("IRepository", ex.Message);
    }

    private sealed class FactoryUnitOfWork(TestDbContext dbContext)
        : UnitOfWork<Guid>(dbContext, Guid.Empty)
    {
        public IRepository<Employee, int> Employees => GetRepository<Employee, int>();

        // Exposes ResolveRepository for the "without SP" test
        public IRepository<Employee, int> TryResolve() => ResolveRepository<IRepository<Employee, int>>();
    }

    private sealed class DiUnitOfWork(TestDbContext dbContext, IServiceProvider sp)
        : UnitOfWork<Guid>(dbContext, Guid.Empty, sp)
    {
        public IRepository<Employee, int> GetEmployees() => ResolveRepository<IRepository<Employee, int>>();
    }
}

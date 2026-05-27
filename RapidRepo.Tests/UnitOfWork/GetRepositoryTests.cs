using Microsoft.EntityFrameworkCore;
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

    private sealed class FactoryUnitOfWork(TestDbContext dbContext)
        : UnitOfWork<Guid>(dbContext, Guid.Empty)
    {
        public RapidRepo.Repositories.Interfaces.IRepository<Employee, int> Employees
            => GetRepository<Employee, int>();
    }
}

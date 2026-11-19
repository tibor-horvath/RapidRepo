using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.Repository;

public class RepositoryTests : IDisposable
{
    private readonly TestDbContext _dbContext;
    private readonly EfRepository<Employee, int> _sut;

    public RepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"RepositoryTests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new TestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new EfRepository<Employee, int>(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_DoesNotThrow()
    {
        var act = () => new EfRepository<Employee, int>(_dbContext);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task Add_ThenGetAll_ReturnsAddedEntity()
    {
        var employee = new Employee
        {
            FirstName = "Jane",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        await _dbContext.SaveChangesAsync();

        var result = _sut.GetAll();

        result.Should().ContainSingle(e => e.FirstName == "Jane");
    }
}

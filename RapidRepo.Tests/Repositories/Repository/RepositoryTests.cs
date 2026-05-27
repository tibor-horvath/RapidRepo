using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Repositories;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.Repository;

public class RepositoryTests : IDisposable
{
    private readonly TestDbContext _dbContext;
    private readonly Repository<Employee, int> _sut;

    public RepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"RepositoryTests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new TestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new Repository<Employee, int>(_dbContext);
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
        var act = () => new Repository<Employee, int>(_dbContext);
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

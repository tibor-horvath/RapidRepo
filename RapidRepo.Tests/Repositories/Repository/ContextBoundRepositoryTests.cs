using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Repositories;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.Repository;

/// <summary>
/// <see cref="Repository{TEntity, TId, TContext}"/> differs from the two-parameter form only in taking a
/// derived context, so these cover the binding rather than re-testing the inherited CRUD surface.
/// </summary>
public class ContextBoundRepositoryTests : IDisposable
{
    private readonly TestDbContext _dbContext;
    private readonly Repository<Employee, int, TestDbContext> _sut;

    public ContextBoundRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"ContextBoundRepositoryTests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new TestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _sut = new Repository<Employee, int, TestDbContext>(_dbContext);
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
        var act = () => new Repository<Employee, int, TestDbContext>(_dbContext);
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_NullContext_Throws()
    {
        var act = () => new Repository<Employee, int, TestDbContext>(null!);
        act.Should().Throw<ArgumentNullException>();
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

    /// <summary>
    /// The whole point of the type: it operates on the context it was handed, sharing that change tracker.
    /// </summary>
    [Fact]
    public void Add_TracksOnTheSuppliedContext()
    {
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Smith",
            DateOfBirth = new DateTime(1985, 6, 15),
        };

        _sut.Add(employee);

        _dbContext.ChangeTracker.Entries<Employee>()
            .Should().ContainSingle(e => e.State == EntityState.Added);
    }

    [Fact]
    public void IsAssignableToTheSameAbstractions_AsTheTwoParameterForm()
    {
        _sut.Should().BeAssignableTo<BaseRepository<Employee, int>>();
    }
}

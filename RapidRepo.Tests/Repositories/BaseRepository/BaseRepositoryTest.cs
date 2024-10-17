using AutoFixture;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.TestData;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;

public class BaseRepositoryTest : IDisposable
{
    protected readonly IFixture _fixture;

    internal TestDbContext _dbContext;
    internal EmployeeRepository _sut;

    protected BaseRepositoryTest(bool setQueryFilter = false)
    {
        _fixture = new Fixture();

        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDatabase-{Guid.NewGuid()}")
        .EnableSensitiveDataLogging()
            .Options;
        if (_dbContext != null)
        {
            _dbContext.Database.EnsureDeleted();
        }
        _dbContext = new TestDbContext(options, setQueryFilter);
        _sut = new EmployeeRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}

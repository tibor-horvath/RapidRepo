using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.TestData;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;

public class BaseRepositoryTest : IDisposable
{
    internal TestDbContext _dbContext;
    internal EmployeeRepository _sut;

    protected BaseRepositoryTest(bool setQueryFilter = false)
    {
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

    protected void DetachAllEntities()
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries())
        {
            entry.State = EntityState.Detached;
        }
    }
}

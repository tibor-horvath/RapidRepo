using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.Repositories.ReadOnlyRepository.TestData;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;

public class BaseReadOnlyRepositoryTest : IDisposable
{
    internal TestDbContext _dbContext;
    internal ReadOnlyEmployeeRepository _sut;

    protected BaseReadOnlyRepositoryTest()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDatabase-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        _dbContext?.Database.EnsureDeleted();

        _dbContext = new TestDbContext(options);

        _dbContext.Database.EnsureCreated();

        _sut = new ReadOnlyEmployeeRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    protected void DetachAllEntities()
    {
        var changedEntries = _dbContext.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added ||
                        e.State == EntityState.Modified ||
                        e.State == EntityState.Deleted ||
                        e.State == EntityState.Unchanged)
            .ToList();

        foreach (var entry in changedEntries)
        {
            entry.State = EntityState.Detached;
        }
    }
}

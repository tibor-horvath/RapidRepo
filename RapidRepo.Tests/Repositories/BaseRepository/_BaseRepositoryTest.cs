﻿using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.Repositories.BaseRepository.TestData;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;

public class BaseWriteRepositoryTest : IDisposable
{
    internal TestDbContext _dbContext;
    internal EmployeeRepository _sut;

    protected BaseWriteRepositoryTest()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDatabase-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        _dbContext?.Database.EnsureDeleted();

        _dbContext = new TestDbContext(options);

        _dbContext.Database.EnsureCreated();

        _sut = new EmployeeRepository(_dbContext);
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

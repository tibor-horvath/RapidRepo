using AutoFixture;
using Microsoft.EntityFrameworkCore;
using Repository.Tests.TestData;
using RapidRepo.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;

public class BaseRepositoryTest : IDisposable
{
    protected readonly IFixture _fixture;

    internal TestDbContext _dbContext;
    internal EmployeeRepository _sut;

    public BaseRepositoryTest()
    {
        _fixture = new Fixture();

        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDatabase-{Guid.NewGuid()}")
        .EnableSensitiveDataLogging()
            .Options;
        _dbContext = new TestDbContext(options);
        _sut = new EmployeeRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}

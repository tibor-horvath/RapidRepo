using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.Repository;

/// <summary>
/// The write operations of <see cref="EfRepository{TEntity, TId}"/> that the write rules call from other
/// operations route through the repository, so a custom repository's overrides see those calls too.
/// </summary>
public class RepositoryOverrideTests : IDisposable
{
    private readonly TestDbContext _dbContext;

    public RepositoryOverrideTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"RepositoryOverrideTests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new TestDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Delete_OfAnEntityWithoutSoftDelete_GoesThroughTheHardDeleteOverride()
    {
        var token = Seed(new AccessToken { Id = "t1", Value = "v" });
        var sut = new RecordingRepository<AccessToken, string>(_dbContext);

        sut.Delete(token);

        sut.Calls.Should().Equal("Delete", "HardDelete");
        _dbContext.Entry(token).State.Should().Be(EntityState.Deleted);
    }

    [Fact]
    public void DeleteById_GoesThroughTheDeleteAndHardDeleteOverrides()
    {
        Seed(new AccessToken { Id = "t1", Value = "v" });
        var sut = new RecordingRepository<AccessToken, string>(_dbContext);

        sut.DeleteById("t1");

        sut.Calls.Should().Equal("Delete", "HardDelete");
    }

    [Fact]
    public void DeleteRange_OfEntitiesWithoutSoftDelete_GoesThroughTheHardDeleteRangeOverride()
    {
        var token = Seed(new AccessToken { Id = "t1", Value = "v" });
        var sut = new RecordingRepository<AccessToken, string>(_dbContext);

        sut.DeleteRange([token]);

        sut.Calls.Should().Equal("HardDeleteRange");
    }

    [Fact]
    public void HardDeleteById_GoesThroughTheHardDeleteOverride()
    {
        Seed(new AccessToken { Id = "t1", Value = "v" });
        var sut = new RecordingRepository<AccessToken, string>(_dbContext);

        sut.HardDeleteById("t1");

        sut.Calls.Should().Equal("HardDelete");
    }

    [Fact]
    public void RestoreById_GoesThroughTheRestoreOverride()
    {
        var company = Seed(new Company { Id = Guid.NewGuid(), Name = "Acme", DeletedAt = DateTime.UtcNow });
        var sut = new RecordingRepository<Company, Guid>(_dbContext);

        sut.RestoreById(company.Id);

        sut.Calls.Should().Equal("Restore");
        company.DeletedAt.Should().BeNull();
    }

    private T Seed<T>(T entity) where T : class
    {
        _dbContext.Add(entity);
        _dbContext.SaveChanges();
        return entity;
    }

    private sealed class RecordingRepository<TEntity, TId>(DbContext dbContext) : EfRepository<TEntity, TId>(dbContext)
        where TEntity : Entities.BaseEntity<TId>
        where TId : notnull
    {
        public List<string> Calls { get; } = [];

        public override void Delete(TEntity entity)
        {
            Calls.Add(nameof(Delete));
            base.Delete(entity);
        }

        public override void HardDelete(TEntity entity)
        {
            Calls.Add(nameof(HardDelete));
            base.HardDelete(entity);
        }

        public override void HardDeleteRange(IEnumerable<TEntity> entities)
        {
            Calls.Add(nameof(HardDeleteRange));
            base.HardDeleteRange(entities);
        }

        public override void Restore(TEntity entity)
        {
            Calls.Add(nameof(Restore));
            base.Restore(entity);
        }
    }
}

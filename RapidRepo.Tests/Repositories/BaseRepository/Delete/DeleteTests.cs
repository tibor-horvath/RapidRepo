using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository.Delete;
public class DeleteTests
{
    internal TestDbContext _dbContext;
    private readonly CompanyRepository _sut;

    public DeleteTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDatabase-{Guid.NewGuid()}")
        .EnableSensitiveDataLogging()
            .Options;
        if (_dbContext != null)
        {
            _dbContext.Database.EnsureDeleted();
        }
        _dbContext = new TestDbContext(options);
        _sut = new CompanyRepository(_dbContext);
    }

    [Fact]
    public void Delete_ShouldRemoveEntityFromDbContext()
    {
        // Arrange        
        var company = new Company
        {
            Name = "Company1",
        };

        _sut.Add(company);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        _sut.Delete(company);
        _dbContext.SaveChanges();

        // Assert
        var deletedCompany = _dbContext.Companies.Find(company.Id);
        deletedCompany.Should().BeNull();
    }
    private void DetachAllEntities()
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries())
        {
            entry.State = EntityState.Detached;
        }
    }
}

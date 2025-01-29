using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository.Delete;
public class DeleteAsyncTests
{
    internal TestDbContext _dbContext;
    private readonly CompanyRepository _sut;

    public DeleteAsyncTests()
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
    public void Delete_ShouldRemoveEntitiesFromDbContext()
    {
        // Arrange
        var company1 = new Company
        {
            Name = "Company1",
        };

        var company2 = new Company
        {
            Name = "Company2",
        };

        _sut.Add(company1);
        _sut.Add(company2);
        _dbContext.SaveChanges();


        var companiesToRemove = new List<Company> { company1, company2 };

        // Act
        _sut.Delete(companiesToRemove);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        var deletedCompany1 = _dbContext.Companies.Find(company1.Id);
        var deletedCompany2 = _dbContext.Companies.Find(company2.Id);

        deletedCompany1.Should().BeNull();
        deletedCompany2.Should().BeNull();
    }


    private void DetachAllEntities()
    {
        foreach (var entry in _dbContext.ChangeTracker.Entries())
        {
            entry.State = EntityState.Detached;
        }
    }
}

using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository.Delete;
public class DeleteAsyncTests
{
    private readonly IFixture _fixture;
    internal TestDbContext _dbContext;
    private readonly CompanyRepository _sut;

    public DeleteAsyncTests()
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
        _dbContext = new TestDbContext(options);
        _sut = new CompanyRepository(_dbContext);
    }

    [Fact]
    public void Delete_ShouldRemoveEntitiesFromDbContext()
    {
        // Arrange
        var company1 = _fixture
            .Build<Company>()
            .Without(e => e.Id)
            .Create();

        var company2 = _fixture
            .Build<Company>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(company1);
        _sut.Add(company2);
        _dbContext.SaveChanges();

        var companiesToRemove = new List<Company> { company1, company2 };

        // Act
        _sut.Delete(companiesToRemove);
        _dbContext.SaveChanges();

        // Assert
        var deletedCompany1 = _dbContext.Companies.Find(company1.Id);
        var deletedCompany2 = _dbContext.Companies.Find(company2.Id);

        deletedCompany1.Should().BeNull();
        deletedCompany2.Should().BeNull();
    }
}

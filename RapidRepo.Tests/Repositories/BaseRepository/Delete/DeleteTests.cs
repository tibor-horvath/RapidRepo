using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository.Delete;
public class DeleteTests
{
    private readonly IFixture _fixture;
    internal TestDbContext _dbContext;
    private readonly CompanyRepository _sut;

    public DeleteTests()
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
    public void Delete_ShouldRemoveEntityFromDbContext()
    {
        // Arrange
        var company = _fixture
            .Build<Company>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(company);
        _dbContext.SaveChanges();

        // Act
        _sut.Delete(company);
        _dbContext.SaveChanges();

        // Assert
        var deletedCompany = _dbContext.Companies.Find(company.Id);
        deletedCompany.Should().BeNull();
    }
}

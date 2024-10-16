using AutoFixture;
using FluentAssertions;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class GetAllTests : BaseRepositoryTest
{
    [Fact]
    public void GetAll_ShouldReturnAllEntities()
    {
        // Arrange
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .With(e => e.FirstName, "FirstName1")
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .With(e => e.FirstName, "FirstName2")
            .With(e => e.Manager, employee1)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.GetAll(selector: e => new { e.FirstName });

        // Assert
        result.Should().Contain(e => e.FirstName == "FirstName1");
        result.Should().Contain(e => e.FirstName == "FirstName2");
    }

    [Fact]
    public void GetAll_ShouldReturnColumnsForAll()
    {
        // Arrange
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.GetAll();

        // Assert
        result.Should().Contain(employee1);
        result.Should().Contain(employee2);
    }

    [Fact]
    public void GetAll_ShouldReturnAllEntitiesWithManager()
    {
        // Arrange
        var employee1 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .With(e => e.FirstName, "FirstName1")
            .Create();

        var employee2 = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .With(e => e.FirstName, "FirstName2")
            .With(e => e.Manager, employee1)
            .Create();

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.GetAll(selector: e => new { e.FirstName, e.Manager });

        // Assert
        result.Should().Contain(e => e.FirstName == "FirstName1" && e.Manager == null);
        result.Should().Contain(e => e.FirstName == "FirstName2" && e.Manager != null);
    }
}

using AutoFixture;
using FluentAssertions;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class GetAllAsyncTests : BaseRepositoryTest
{
    [Fact]
    public async void GetAllAsync_ShouldReturnAllEntities()
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
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().Contain(employee1);
        result.Should().Contain(employee2);
    }

    [Fact]
    public async void GetAllAsync_Select_ShouldReturnAllEntities()
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
        var result = await _sut.GetAllAsync(selector: e => new { e.FirstName });

        // Assert
        result.Should().Contain(e => e.FirstName == "FirstName1");
        result.Should().Contain(e => e.FirstName == "FirstName2");
    }
}

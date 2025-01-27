using FluentAssertions;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class GetAllAsyncTests : BaseRepositoryTest
{
    [Fact]
    public async Task GetAllAsync_ShouldReturnAllEntities()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "TestFirstName",
            LastName = "TestLastName",
        };

        var employee2 = new Employee
        {
            FirstName = "TestFirstName2",
            LastName = "TestLastName2",
            Manager = employee1
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().ContainEquivalentOf(employee1);
        result.Should().ContainEquivalentOf(employee2);
    }

    [Fact]
    public async Task GetAllAsync_Select_ShouldReturnAllEntities()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "FirstName1",
            LastName = "TestLastName",
        };

        var employee2 = new Employee
        {
            FirstName = "FirstName2",
            LastName = "TestLastName2",
            Manager = employee1
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetAllAsync(selector: e => new { e.FirstName });

        // Assert
        result.Should().Contain(e => e.FirstName == "FirstName1");
        result.Should().Contain(e => e.FirstName == "FirstName2");
    }
}

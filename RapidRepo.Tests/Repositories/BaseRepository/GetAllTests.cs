using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class GetAllTests : BaseWriteRepositoryTest
{
    [Fact]
    public void GetAll_ShouldReturnAllEntities()
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
        var result = _sut.GetAll(selector: e => new { e.FirstName });

        // Assert
        result.Should().Contain(e => e.FirstName == "FirstName1");
        result.Should().Contain(e => e.FirstName == "FirstName2");
    }

    [Fact]
    public void GetAll_ShouldReturnColumnsForAll()
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
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetAll();

        // Assert
        result.Should().ContainEquivalentOf(employee1);
        result.Should().ContainEquivalentOf(employee2);
    }

    [Fact]
    public void GetAll_ShouldReturnAllEntitiesWithManager()
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
        var result = _sut.GetAll(selector: e => new { e.FirstName, e.Manager });

        // Assert
        result.Should().Contain(e => e.FirstName == "FirstName1" && e.Manager == null);
        result.Should().Contain(e => e.FirstName == "FirstName2" && e.Manager != null);
    }
}

using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;
public class GetFirstOrDefaultAsyncTests : BaseReadOnlyRepositoryTest
{
    [Fact]
    public async Task GetFirstOrDefaultAsync_ShouldReturnFirstEntity()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee2 = new Employee
        {
            FirstName = "Luke",
            LastName = "Lucky",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _dbContext.Employees.Add(employee1);
        _dbContext.Employees.Add(employee2);

        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetFirstOrDefaultAsync();

        // Assert
        result.Should().BeEquivalentTo(employee1);
    }

    [Fact]
    public async Task GetFirstOrDefaultAsync_ShouldReturnDefaultEntity()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee2 = new Employee
        {
            FirstName = "Luke",
            LastName = "Lucky",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _dbContext.Employees.Add(employee1);
        _dbContext.Employees.Add(employee2);

        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetFirstOrDefaultAsync(x => x.FirstName == "Jack");

        // Assert
        result.Should().BeNull();
    }
}

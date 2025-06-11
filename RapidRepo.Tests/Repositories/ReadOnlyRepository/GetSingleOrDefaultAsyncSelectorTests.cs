using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;
public class GetSingleOrDefaultAsyncSelectorTests : BaseReadOnlyRepositoryTest
{
    [Fact]
    public async Task GetSingleOrDefault_WithSelector_ShouldReturnSingleEntity()
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
        var result = await _sut.GetSingleOrDefaultAsync(e => new { e.FirstName, e.LastName }, x => x.FirstName == "John");

        // Assert
        result.Should().BeEquivalentTo(new { employee1.FirstName, employee1.LastName });
    }

    [Fact]
    public async Task GetSingleOrDefault_WithSelector_ShouldReturnDefaultEntity()
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
        var result = await _sut.GetSingleOrDefaultAsync(e => new { e.FirstName, e.LastName }, x => x.FirstName == "Jack");

        // Assert
        result.Should().BeNull();
    }
}

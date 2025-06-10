using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;
public class AnyAsyncTests : BaseReadOnlyRepositoryTest
{
    [Fact]
    public async Task AnyAsync_ShouldReturnTrue_WhenConditionIsMet()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();

        // Act
        var result = await _sut.AnyAsync(e => e.Id == employee.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AnyAsync_ShouldReturnFalse_WhenConditionIsNotMet()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();

        // Act
        var result = await _sut.AnyAsync(e => e.Id == employee.Id + 1);

        // Assert
        result.Should().BeFalse();
    }
}

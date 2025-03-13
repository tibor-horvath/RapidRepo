using FluentAssertions;
using Repository.Tests.TestData;
using System.Linq.Expressions;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class CountTests : BaseRepositoryTest
{
    [Fact]
    public void Count_ShouldReturnCorrectCount_WhenConditionIsMet()
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
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();

        DetachAllEntities();

        Expression<Func<Employee, bool>> condition = e => e.Id == employee1.Id || e.Id == employee2.Id;

        // Act
        var result = _sut.Count(condition);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public void Count_ShouldReturnZero_WhenConditionIsNotMet()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();

        DetachAllEntities();

        Expression<Func<Employee, bool>> condition = e => e.Id == employee.Id + 1;

        // Act
        var result = _sut.Count(condition);

        // Assert
        result.Should().Be(0);
    }
}

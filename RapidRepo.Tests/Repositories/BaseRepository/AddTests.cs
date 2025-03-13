using FluentAssertions;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;

public class AddTests : BaseRepositoryTest
{
    [Fact]
    public void Add_ShouldAddEntityToDbContext()
    {
        // Arrange       
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        // Act
        _sut.Add(employee);
        _dbContext.SaveChanges();

        // Assert
        var addedEmployee = _dbContext.Employees.SingleOrDefault();
        addedEmployee.Should().NotBeNull();
        addedEmployee.Should().BeEquivalentTo(employee);
    }

    [Fact]
    public void Add_ShouldAddEntitiesToDbContext()
    {
        // Arrange
        var numberOfEmployeesToAdd = 5;

        var employees = new List<Employee>();

        for (var i = 0; i < numberOfEmployeesToAdd; i++)
        {
            employees.Add(new Employee
            {
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = new DateTime(1990, 1, 1)
            });
        }


        // Act
        _sut.AddRange(employees);
        _dbContext.SaveChanges();

        // Assert
        _dbContext.Employees.Count().Should().Be(numberOfEmployeesToAdd);
        _dbContext.Employees.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }
}

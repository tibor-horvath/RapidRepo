using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;
public class GetByIdTests : BaseReadOnlyRepositoryTest
{
    [Fact]
    public void GetById_ShouldReturnEntityWithMatchingId()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "TestFirstName",
            LastName = "TestLastName",
        };

        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetById(employee.Id);

        // Assert
        result.Should().BeEquivalentTo(employee);
    }

    [Fact]
    public void GetById_ShouldReturnEntityWithMatchingIdWithFirstName()
    {
        // Arrange
        var fistNameExpected = "TestFirstName";
        var testEmployeeId = 78;

        var employee = new Employee
        {
            Id = testEmployeeId,
            FirstName = fistNameExpected,
            LastName = "TestLastName",
        };

        var employees = new List<Employee>
        {
            new Employee { Id = 1, FirstName = "FirstName1", LastName = "LastName1" },
            new Employee { Id = 2, FirstName = "FirstName2", LastName = "LastName2" },
            new Employee { Id = 3, FirstName = "FirstName3", LastName = "LastName3" },
            new Employee { Id = 4, FirstName = "FirstName4", LastName = "LastName4" }
        };

        employees.Add(employee);

        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetById(id: testEmployeeId, selector: e => e.FirstName);

        // Assert
        result.Should().Be(fistNameExpected);
    }

    [Fact]
    public void GetById_ShouldReturnEntityWithMatchingIdWithFirstAndLastName()
    {
        // Arrange
        var fistNameExpected = "TestFirstName";
        var lastNameExpected = "TestLastName";
        var testEmployeeId = 78;

        var employee = new Employee
        {
            Id = testEmployeeId,
            FirstName = fistNameExpected,
            LastName = lastNameExpected
        };

        var employees = new List<Employee>
        {
            new Employee { Id = 1, FirstName = "FirstName1", LastName = "LastName1" },
            new Employee { Id = 2, FirstName = "FirstName2", LastName = "LastName2" },
            new Employee { Id = 3, FirstName = "FirstName3", LastName = "LastName3" },
            new Employee { Id = 4, FirstName = "FirstName4", LastName = "LastName4" }
        };

        employees.Add(employee);

        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetById(id: testEmployeeId, selector: e => new { e.FirstName, e.LastName });

        // Assert
        result.Should().Be(new { FirstName = fistNameExpected, LastName = lastNameExpected });
    }
}

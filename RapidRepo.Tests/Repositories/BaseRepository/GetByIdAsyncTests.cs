using FluentAssertions;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class GetByIdAsyncTests : BaseRepositoryTest
{
    [Fact]
    public async Task GetByIdAsync_ShouldReturnEntityWithMatchingId()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "TestFirstName",
            LastName = "TestLastName",
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetByIdAsync(employee.Id);

        // Assert
        result.Should().BeEquivalentTo(employee);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnEntityWithMatchingIdWithFirstName()
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

        _sut.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetByIdAsync(id: testEmployeeId, selector: e => e.FirstName);

        // Assert
        result.Should().Be(fistNameExpected);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnEntityWithMatchingIdWithFirstAndLastName()
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

        _sut.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetByIdAsync(id: testEmployeeId, selector: e => new { e.FirstName, e.LastName });

        // Assert
        result.Should().BeEquivalentTo(new { FirstName = fistNameExpected, LastName = lastNameExpected });
    }
}

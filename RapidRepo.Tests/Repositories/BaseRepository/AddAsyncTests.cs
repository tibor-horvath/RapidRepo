using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class AddAsyncTests : BaseWriteRepositoryTest
{
    [Fact]
    public async Task AddAsync_ShouldAddEntityToDbContext()
    {
        // Arrange       
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        // Act
        await _sut.AddAsync(employee);
        await _dbContext.SaveChangesAsync();

        // Assert
        var addedEmployee = _dbContext.Employees.SingleOrDefault();
        addedEmployee.Should().NotBeNull();
        addedEmployee.Should().BeEquivalentTo(employee);
    }

    [Fact]
    public async Task AddAsync_ShouldAddEntitiesToDbContext()
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
        await _sut.AddAsync(employees);
        await _dbContext.SaveChangesAsync();

        // Assert
        _dbContext.Employees.Count().Should().Be(numberOfEmployeesToAdd);
        _dbContext.Employees.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }
}

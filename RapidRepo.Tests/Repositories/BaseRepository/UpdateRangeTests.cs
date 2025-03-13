using Microsoft.EntityFrameworkCore;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class UpdateRangeTests : BaseRepositoryTest
{
    [Fact]
    public async Task UpdateRange_UpdatesEntities()
    {
        // Arrange
        var employees = new List<Employee>
            {
                new Employee { FirstName = "John", LastName = "Doe"},
                new Employee { FirstName = "Jane", LastName = "Smith"}
            };

        _dbContext.Employees.AddRange(employees);
        await _dbContext.SaveChangesAsync();

        // Act
        employees[0].FirstName = "John Updated";
        employees[1].LastName = "Smith Updated";
        _sut.UpdateRange(employees);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updatedEmployees = await _dbContext.Employees.ToListAsync();
        Assert.Equal("John Updated", updatedEmployees.First(e => e.Id == 1).FirstName);
        Assert.Equal("Smith Updated", updatedEmployees.First(e => e.Id == 2).LastName);
    }

    [Fact]
    public async Task UpdateRange_AddNewEntities()
    {
        // Arrange
        var employees = new List<Employee>
            {
                new Employee { FirstName = "John", LastName = "Doe"},
                new Employee { FirstName = "Jane", LastName = "Smith"}
            };

        _dbContext.Employees.AddRange(employees);
        await _dbContext.SaveChangesAsync();

        // Act
        employees.Add(new Employee { FirstName = "Jack", LastName = "Doe" });
        _sut.UpdateRange(employees);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updatedEmployees = await _dbContext.Employees.ToListAsync();
        Assert.Equal(employees[0], updatedEmployees.First(e => e.Id == 1));
        Assert.Equal(employees[1], updatedEmployees.First(e => e.Id == 2));
        Assert.Equal(employees[2], updatedEmployees.First(e => e.Id == 3));
    }
}

using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class DeleteByIdTests : BaseWriteRepositoryTest
{
    [Fact]
    public async Task DeleteById_ShouldDeleteEntity_WhenEntityExists()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };
        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync();

        // Act
        _sut.DeleteById(employee.Id);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Assert
        var deletedEmployee = await _dbContext.Employees.FindAsync(employee.Id);
        Assert.Null(deletedEmployee);
    }

    [Fact]
    public async Task DeleteById_ShouldNotThrow_WhenEntityDoesNotExist()
    {
        // Arrange
        var nonExistentId = 999;

        // Act
        var exception = await Record.ExceptionAsync(() => Task.Run(() => _sut.DeleteById(nonExistentId)));

        // Assert
        Assert.Null(exception);
    }
}

using RapidRepo.Tests.Repositories.BaseRepository.TestData;
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

    [Fact]
    public async Task DeleteById_ShouldDeleteEntity_WhenStringKeyIsUsed()
    {
        // Arrange
        var repository = new AccessTokenRepository(_dbContext);
        var token = new AccessToken
        {
            Id = "token-1",
            Value = "abc123"
        };

        _dbContext.AccessTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        // Act
        repository.DeleteById(token.Id);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Assert
        var deletedToken = await _dbContext.AccessTokens.FindAsync(token.Id);
        Assert.Null(deletedToken);
    }
}

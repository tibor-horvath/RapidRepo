using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;
using RapidRepo.Tests.Repositories.WriteRepository.TestData;

namespace RapidRepo.Tests.Repositories.WriteRepository;
public class RestoreByIdTests : BaseWriteRepositoryTest
{
    [Fact]
    public void RestoreById_ShouldRestoreEntity_WhenEntityIsHiddenByTheQueryFilter()
    {
        // Arrange
        var employee = CreateEmployee();
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();

        _sut.Delete(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        _sut.RestoreById(employee.Id);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.FirstOrDefault(e => e.Id == employee.Id).Should().NotBeNull();
    }

    [Fact]
    public async Task RestoreByIdAsync_ShouldRestoreEntity_WhenEntityIsHiddenByTheQueryFilter()
    {
        // Arrange
        var employee = CreateEmployee();
        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync();

        _sut.Delete(employee);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Act
        await _sut.RestoreByIdAsync(employee.Id);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.FirstOrDefault(e => e.Id == employee.Id).Should().NotBeNull();
    }

    [Fact]
    public void RestoreById_ShouldNotThrow_WhenEntityDoesNotExist()
    {
        // Act
        var act = () => _sut.RestoreById(999);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task RestoreByIdAsync_ShouldNotThrow_WhenEntityDoesNotExist()
    {
        // Act
        var act = () => _sut.RestoreByIdAsync(999);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void RestoreById_ShouldThrow_WhenEntityDoesNotSupportSoftDelete()
    {
        // Arrange
        var sut = new WriteAccessTokenRepository(_dbContext);

        // Act
        var act = () => sut.RestoreById("token");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(AccessToken)}*");
    }

    [Fact]
    public void RestoreById_ShouldNotRestoreEntity_WhenQueryFiltersAreRespected()
    {
        // Arrange
        var employee = CreateEmployee();
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();

        _sut.Delete(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        _sut.RestoreById(employee.Id, ignoreQueryFilters: false);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.FirstOrDefault(e => e.Id == employee.Id).Should().BeNull();
    }

    private static Employee CreateEmployee() => new()
    {
        FirstName = "John",
        LastName = "Doe",
        DateOfBirth = new DateTime(1990, 1, 1),
    };
}

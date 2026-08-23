using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.Repositories.BaseRepository.TestData;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class RestoreTests : BaseWriteRepositoryTest
{
    [Fact]
    public void Restore_ShouldMakeEntityVisibleAgain_WhenEntityWasSoftDeleted()
    {
        // Arrange
        var employee = CreateEmployee();
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();

        _sut.Delete(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        _sut.Restore(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.FirstOrDefault(e => e.Id == employee.Id).Should().NotBeNull();
    }

    [Fact]
    public void RestoreRange_ShouldRestoreAllEntities_WhenEntitiesWereSoftDeleted()
    {
        // Arrange
        var employees = new[] { CreateEmployee(), CreateEmployee() };
        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();

        _sut.DeleteRange(employees);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        _sut.RestoreRange(employees);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.Should().HaveCount(2);
    }

    [Fact]
    public void RestoreById_ShouldRestoreEntity_WhenEntityIsHiddenByTheQueryFilter()
    {
        // Arrange
        var employee = CreateEmployee();
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();

        _sut.DeleteById(employee.Id);
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

        await _sut.DeleteByIdAsync(employee.Id);
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
    public void Restore_ShouldThrow_WhenEntityDoesNotSupportSoftDelete()
    {
        // Arrange
        var sut = new AccessTokenRepository(_dbContext);
        var accessToken = new AccessToken { Id = "token", Value = "value" };

        // Act
        var act = () => sut.Restore(accessToken);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(AccessToken)}*");
    }

    private static Employee CreateEmployee() => new()
    {
        FirstName = "John",
        LastName = "Doe",
        DateOfBirth = new DateTime(1990, 1, 1),
    };
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.Repositories.BaseRepository.TestData;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class HardDeleteTests : BaseWriteRepositoryTest
{
    [Fact]
    public void HardDelete_ShouldRemoveEntityPermanently_WhenEntitySupportsSoftDelete()
    {
        // Arrange
        var employee = CreateEmployee();
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();

        // Act
        _sut.HardDelete(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.IgnoreQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public void HardDeleteRange_ShouldRemoveAllEntitiesPermanently()
    {
        // Arrange
        var employees = new[] { CreateEmployee(), CreateEmployee() };
        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();

        // Act
        _sut.HardDeleteRange(employees);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.IgnoreQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public void HardDeleteById_ShouldRemoveEntityPermanently_WhenEntityWasAlreadySoftDeleted()
    {
        // Arrange
        var employee = CreateEmployee();
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();

        _sut.Delete(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        _sut.HardDeleteById(employee.Id, ignoreQueryFilters: true);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.IgnoreQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public async Task HardDeleteByIdAsync_ShouldRemoveEntityPermanently_WhenEntityWasAlreadySoftDeleted()
    {
        // Arrange
        var employee = CreateEmployee();
        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync();

        _sut.Delete(employee);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Act
        await _sut.HardDeleteByIdAsync(employee.Id, ignoreQueryFilters: true);
        await _dbContext.SaveChangesAsync();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.IgnoreQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public void HardDelete_ShouldRemoveEntity_WhenEntityDoesNotSupportSoftDelete()
    {
        // Arrange
        var sut = new AccessTokenRepository(_dbContext);
        var accessToken = new AccessToken { Id = "token", Value = "value" };
        _dbContext.AccessTokens.Add(accessToken);
        _dbContext.SaveChanges();

        // Act
        sut.HardDelete(accessToken);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.AccessTokens.Should().BeEmpty();
    }

    private static Employee CreateEmployee() => new()
    {
        FirstName = "John",
        LastName = "Doe",
        DateOfBirth = new DateTime(1990, 1, 1),
    };
}

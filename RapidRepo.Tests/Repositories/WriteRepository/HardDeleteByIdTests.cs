using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.Repositories.TestData;
using RapidRepo.Tests.Repositories.WriteRepository.TestData;

namespace RapidRepo.Tests.Repositories.WriteRepository;
public class HardDeleteByIdTests : BaseWriteRepositoryTest
{
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
    public void HardDeleteById_ShouldRemoveEntity_WhenEntityDoesNotSupportSoftDelete()
    {
        // Arrange
        var sut = new WriteAccessTokenRepository(_dbContext);
        _dbContext.AccessTokens.Add(new AccessToken { Id = "token", Value = "value" });
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        sut.HardDeleteById("token");
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.AccessTokens.Should().BeEmpty();
    }

    [Fact]
    public void HardDeleteById_ShouldNotThrow_WhenEntityDoesNotExist()
    {
        // Act
        var act = () => _sut.HardDeleteById(999);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task HardDeleteByIdAsync_ShouldNotThrow_WhenEntityDoesNotExist()
    {
        // Act
        var act = () => _sut.HardDeleteByIdAsync(999);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void HardDeleteById_ShouldNotRemoveEntity_WhenEntityIsHiddenByTheQueryFilterAndFiltersAreRespected()
    {
        // Arrange
        var employee = CreateEmployee();
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();

        _sut.Delete(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        _sut.HardDeleteById(employee.Id);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.IgnoreQueryFilters().Should().ContainSingle();
    }

    [Fact]
    public void HardDeleteById_ShouldRemoveEntity_WhenEntityIsStagedButNotYetCommitted()
    {
        // Arrange
        var employee = CreateEmployee();
        _sut.Add(employee);

        // Act
        _sut.HardDeleteById(employee.Id);

        // Assert
        _dbContext.Entry(employee).State.Should().NotBe(EntityState.Added);
    }

    private static Employee CreateEmployee() => new()
    {
        FirstName = "John",
        LastName = "Doe",
        DateOfBirth = new DateTime(1990, 1, 1),
    };
}

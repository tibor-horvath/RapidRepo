using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.Repositories.TestData;
using RapidRepo.Tests.Repositories.WriteRepository.TestData;

namespace RapidRepo.Tests.Repositories.WriteRepository;
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
        _dbContext.Employees
            .IgnoreQueryFilters()
            .FirstOrDefault(e => e.Id == employee.Id)
            .Should().BeNull();
    }

    [Fact]
    public void HardDelete_ShouldRemoveEntityPermanently_WhenEntityWasAlreadySoftDeleted()
    {
        // Arrange
        var employee = CreateEmployee();
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();

        _sut.Delete(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        var softDeleted = _dbContext.Employees
            .IgnoreQueryFilters()
            .Single(e => e.Id == employee.Id);

        // Act
        _sut.HardDelete(softDeleted);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.IgnoreQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public void HardDelete_ShouldRemoveEntity_WhenEntityDoesNotSupportSoftDelete()
    {
        // Arrange
        var sut = new WriteAccessTokenRepository(_dbContext);
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

    [Fact]
    public void HardDelete_ShouldThrow_WhenEntityIsNull()
    {
        // Act
        var act = () => _sut.HardDelete(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static Employee CreateEmployee() => new()
    {
        FirstName = "John",
        LastName = "Doe",
        DateOfBirth = new DateTime(1990, 1, 1),
    };
}

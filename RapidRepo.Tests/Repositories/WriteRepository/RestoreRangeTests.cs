using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.Repositories.TestData;
using RapidRepo.Tests.Repositories.WriteRepository.TestData;

namespace RapidRepo.Tests.Repositories.WriteRepository;
public class RestoreRangeTests : BaseWriteRepositoryTest
{
    [Fact]
    public void RestoreRange_ShouldRestoreAllEntities_WhenEntitiesWereSoftDeleted()
    {
        // Arrange
        var employees = new[] { CreateEmployee("John"), CreateEmployee("Jack") };
        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();

        _sut.DeleteRange(employees);
        _dbContext.SaveChanges();
        DetachAllEntities();

        var softDeleted = _dbContext.Employees.IgnoreQueryFilters().ToList();
        softDeleted.Should().HaveCount(2);

        // Act
        _sut.RestoreRange(softDeleted);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.Should().HaveCount(2);
    }

    [Fact]
    public void RestoreRange_ShouldThrow_WhenEntityDoesNotSupportSoftDelete()
    {
        // Arrange
        var sut = new WriteAccessTokenRepository(_dbContext);
        var accessTokens = new[] { new AccessToken { Id = "token", Value = "value" } };

        // Act
        var act = () => sut.RestoreRange(accessTokens);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(AccessToken)}*");
    }

    [Fact]
    public void RestoreRange_ShouldThrow_WhenEntitiesAreNull()
    {
        // Act
        var act = () => _sut.RestoreRange(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static Employee CreateEmployee(string firstName) => new()
    {
        FirstName = firstName,
        LastName = "Doe",
        DateOfBirth = new DateTime(1990, 1, 1),
    };
}

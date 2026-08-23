using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.Repositories.TestData;
using RapidRepo.Tests.Repositories.WriteRepository.TestData;

namespace RapidRepo.Tests.Repositories.WriteRepository;
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

        var softDeleted = _dbContext.Employees
            .IgnoreQueryFilters()
            .Single(e => e.Id == employee.Id);

        // Act
        _sut.Restore(softDeleted);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        var restored = _dbContext.Employees.FirstOrDefault(e => e.Id == employee.Id);
        restored.Should().NotBeNull();
        restored!.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void Restore_ShouldClearDeletedBy_WhenEntityTracksWhoDeletedIt()
    {
        // Arrange
        var employee = CreateEmployee();
        employee.DeletedAt = DateTime.UtcNow;
        employee.DeletedBy = Guid.NewGuid();

        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        var softDeleted = _dbContext.Employees
            .IgnoreQueryFilters()
            .Single(e => e.Id == employee.Id);

        // Act
        _sut.Restore(softDeleted);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        var restored = _dbContext.Employees
            .IgnoreQueryFilters()
            .Single(e => e.Id == employee.Id);
        restored.DeletedAt.Should().BeNull();
        restored.DeletedBy.Should().BeNull();
    }

    [Fact]
    public void Restore_ShouldRestoreEntity_WhenEntityIsDetached()
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
    public void Restore_ShouldThrow_WhenEntityDoesNotSupportSoftDelete()
    {
        // Arrange
        var sut = new WriteAccessTokenRepository(_dbContext);
        var accessToken = new AccessToken { Id = "token", Value = "value" };

        // Act
        var act = () => sut.Restore(accessToken);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{nameof(AccessToken)}*");
    }

    [Fact]
    public void Restore_ShouldThrow_WhenEntityIsNull()
    {
        // Act
        var act = () => _sut.Restore(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Restore_ShouldReviveEntity_WhenEntityWasStagedForHardDelete()
    {
        // Arrange
        var employee = CreateEmployee();
        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();

        _sut.HardDelete(employee);

        // Act
        _sut.Restore(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.FirstOrDefault(e => e.Id == employee.Id).Should().NotBeNull();
    }

    private static Employee CreateEmployee() => new()
    {
        FirstName = "John",
        LastName = "Doe",
        DateOfBirth = new DateTime(1990, 1, 1),
    };
}

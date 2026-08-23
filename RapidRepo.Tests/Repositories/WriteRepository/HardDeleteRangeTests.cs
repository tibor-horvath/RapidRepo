using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.WriteRepository;
public class HardDeleteRangeTests : BaseWriteRepositoryTest
{
    [Fact]
    public void HardDeleteRange_ShouldRemoveAllEntitiesPermanently_WhenEntitiesSupportSoftDelete()
    {
        // Arrange
        var employees = new[] { CreateEmployee("John"), CreateEmployee("Jack") };
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
    public void HardDeleteRange_ShouldRemoveOnlyTheGivenEntities()
    {
        // Arrange
        var toRemove = CreateEmployee("John");
        var toKeep = CreateEmployee("Jack");
        _dbContext.Employees.AddRange(toRemove, toKeep);
        _dbContext.SaveChanges();

        // Act
        _sut.HardDeleteRange([toRemove]);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        _dbContext.Employees.IgnoreQueryFilters().Should().ContainSingle()
            .Which.Id.Should().Be(toKeep.Id);
    }

    [Fact]
    public void HardDeleteRange_ShouldThrow_WhenEntitiesAreNull()
    {
        // Act
        var act = () => _sut.HardDeleteRange(null!);

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

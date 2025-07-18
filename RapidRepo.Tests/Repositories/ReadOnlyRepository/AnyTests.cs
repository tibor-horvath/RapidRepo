﻿using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;
public class AnyTests : BaseReadOnlyRepositoryTest
{
    [Fact]
    public void Any_ShouldReturnTrue_WhenConditionIsMet()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _dbContext.Employees.AddRange(employee);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.Any(e => e.Id == employee.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Any_ShouldReturnFalse_WhenConditionIsNotMet()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.Any(e => e.Id == employee.Id + 1);

        // Assert
        result.Should().BeFalse();
    }
}

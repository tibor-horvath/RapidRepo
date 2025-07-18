﻿using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;
public class GetSingleOrDefaultTests : BaseReadOnlyRepositoryTest
{
    [Fact]
    public void GetSingleOrDefault_ShouldReturnSingleEntity()
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
        DetachAllEntities();

        // Act
        var result = _sut.GetSingleOrDefault();

        // Assert
        result.Should().BeEquivalentTo(employee);
    }

    [Fact]
    public void GetSingleOrDefault_ShouldReturnDefaultEntity()
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
        DetachAllEntities();

        // Act
        var result = _sut.GetSingleOrDefault(x => x.FirstName == "Jack");

        // Assert
        result.Should().BeNull();
    }
}

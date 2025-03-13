﻿using FluentAssertions;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class GetSingleOrDefaultSelectorTests : BaseRepositoryTest
{
    [Fact]
    public void GetSingleOrDefault_WithSelector_ShouldReturnSingleEntity()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee2 = new Employee
        {
            FirstName = "Luke",
            LastName = "Lucky",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetSingleOrDefault(e => new { e.FirstName, e.LastName }, x => x.FirstName == "John");

        // Assert
        result.Should().BeEquivalentTo(new { employee1.FirstName, employee1.LastName });
    }

    [Fact]
    public void GetSingleOrDefault_WithSelector_ShouldReturnDefaultEntity()
    {
        // Arrange
        var employee1 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee2 = new Employee
        {
            FirstName = "Luke",
            LastName = "Lucky",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = _sut.GetSingleOrDefault(e => new { e.FirstName, e.LastName }, x => x.FirstName == "Jack");

        // Assert
        result.Should().BeNull();
    }
}

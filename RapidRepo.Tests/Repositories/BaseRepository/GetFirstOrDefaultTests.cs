﻿using FluentAssertions;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class GetFirstOrDefaultTests : BaseRepositoryTest
{
    [Fact]
    public void GetFirstOrDefault_ShouldReturnFirstEntity()
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
        var result = _sut.GetFirstOrDefault();

        // Assert
        result.Should().BeEquivalentTo(employee1);
    }

    [Fact]
    public void GetFirstOrDefault_ShouldReturnDefaultEntity()
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
        var result = _sut.GetFirstOrDefault(x => x.FirstName == "Jack");

        // Assert
        result.Should().BeNull();
    }
}

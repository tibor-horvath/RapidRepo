﻿using FluentAssertions;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class GetFirstAsyncSelectorTests : BaseRepositoryTest
{
    [Fact]
    public async Task GetFirstAsync_WithSelector_ShouldReturnFirstEntity()
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
        var result = await _sut.GetFirstAsync(e => new { e.FirstName, e.LastName });

        // Assert
        result.Should().BeEquivalentTo(new { employee1.FirstName, employee1.LastName });
    }
}

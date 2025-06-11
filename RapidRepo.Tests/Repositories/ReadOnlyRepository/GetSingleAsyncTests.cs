using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;
public class GetSingleAsyncTests : BaseReadOnlyRepositoryTest
{
    [Fact]
    public async Task GetSingleAsync_ShouldReturnSingleEntity()
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
        var result = await _sut.GetSingleAsync();

        // Assert
        result.Should().BeEquivalentTo(employee);
    }
}

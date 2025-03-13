using FluentAssertions;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class GetSingleOrDefaultAsyncTests : BaseRepositoryTest
{
    [Fact]
    public async Task GetSingleOrDefaultAsync_ShouldReturnSingleEntity()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetSingleOrDefaultAsync();

        // Assert
        result.Should().BeEquivalentTo(employee);
    }

    [Fact]
    public async Task GetSingleOrDefaultAsync_ShouldReturnDefaultEntity()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetSingleOrDefaultAsync(x => x.FirstName == "Jack");

        // Assert
        result.Should().BeNull();
    }
}

using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class GetPagedAsyncTests : BaseWriteRepositoryTest
{
    [Fact]
    public async Task GetAllPagedAsync_ShouldReturnPagedEntities()
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
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        var employee3 = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _sut.Add(employee1);
        _sut.Add(employee2);
        _sut.Add(employee3);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetAllPagedAsync(pageIndex: 1, pageSize: 2);

        // Assert
        result.Results.Should().ContainEquivalentOf(employee1);
        result.Results.Should().ContainEquivalentOf(employee2);
        result.Results.Should().NotContainEquivalentOf(employee3);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }
}

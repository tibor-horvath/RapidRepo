using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class GetAllAsyncSelectorTests : BaseWriteRepositoryTest
{
    [Fact]
    public async Task GetAllAsync_WithSelector_ReturnsCorrectData()
    {
        // Arrange
        var employees = new[]
        {
            new Employee { FirstName = "John", LastName = "Doe" },
            new Employee { FirstName = "Jane", LastName = "Smith" }
        };
        _dbContext.Employees.AddRange(employees);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = (await _sut.GetAllAsync(e => new { e.Id, e.FirstName, e.LastName })).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Id == 1 && e.FirstName == "John" && e.LastName == "Doe");
        Assert.Contains(result, e => e.Id == 2 && e.FirstName == "Jane" && e.LastName == "Smith");
    }
}

using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;
public class GetAllSelectorTests : BaseReadOnlyRepositoryTest
{
    [Fact]
    public void GetAll_WithSelector_ReturnsCorrectData()
    {
        // Arrange
        var employees = new[]
        {
                new Employee { Id = 1, FirstName = "John", LastName = "Doe" },
                new Employee { Id = 2, FirstName = "Jane", LastName = "Smith" }
            };
        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();

        // Act
        var result = _sut.GetAll(e => new { e.Id, e.FirstName, e.LastName }).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Id == 1 && e.FirstName == "John" && e.LastName == "Doe");
        Assert.Contains(result, e => e.Id == 2 && e.FirstName == "Jane" && e.LastName == "Smith");
    }

    [Fact]
    public async Task GetAllAsync_WithSelector_ReturnsCorrectData()
    {
        // Arrange
        var employees = new[]
        {
                new Employee { Id = 1, FirstName = "John", LastName = "Doe" },
                new Employee { Id = 2, FirstName = "Jane", LastName = "Smith" }
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

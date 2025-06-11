using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.ReadOnlyRepository;
public class GetSingleTests : BaseReadOnlyRepositoryTest
{
    [Fact]
    public void GetSingle_ShouldReturnSingleEntity()
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
        var result = _sut.GetSingle();

        // Assert
        result.Should().BeEquivalentTo(employee);
    }
}

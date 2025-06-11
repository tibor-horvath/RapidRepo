using FluentAssertions;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.WriteRepository;
public class UpdateTests : BaseWriteRepositoryTest
{
    [Fact]
    public void Update_ShouldUpdateEntityInDbContext()
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

        employee.FirstName = "Updated Name";

        // Act
        _sut.Update(employee);
        _dbContext.SaveChanges();

        // Assert
        var updatedEmployee = _dbContext.Employees.Find(employee.Id);
        updatedEmployee.Should().NotBeNull();
        updatedEmployee.Should().BeEquivalentTo(employee);
        updatedEmployee!.FirstName.Should().Be("Updated Name");
    }
}

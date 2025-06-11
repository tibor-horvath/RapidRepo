using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.Repositories.BaseRepository.TestData;
using RapidRepo.Tests.Repositories.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class DeleteRangeTests : BaseWriteRepositoryTest
{
    [Fact]
    public void DeleteRange_HardDelete_RemovesEntities()
    {
        // Arrange
        var employees = new List<Employee>
        {
            new Employee
            {
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = new DateTime(1990, 1, 1),
            },
            new Employee
            {
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = new DateTime(1990, 1, 1),
            }
        };

        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();

        // Act
        _sut.DeleteRange(employees);
        _dbContext.SaveChanges();

        // Assert
        var remainingEmployees = _dbContext.Employees.ToList();
        Assert.Empty(remainingEmployees);
    }

    [Fact]
    public void DeleteRange_SoftDelete_SetsDeletedAt()
    {
        // Arrange
        var employees = new List<Employee>
        {
            new Employee
            {
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = new DateTime(1990, 1, 1),
            },
            new Employee
            {
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = new DateTime(1990, 1, 1),
            }
        };
        _dbContext.Employees.AddRange(employees);
        _dbContext.SaveChanges();

        // Act
        _sut.DeleteRange(employees);
        _dbContext.SaveChanges();

        // Assert
        var deletedEmployees = _dbContext.Employees.IgnoreQueryFilters().Where(e => e.DeletedAt != null).ToList();
        Assert.Equal(2, deletedEmployees.Count);
        Assert.All(deletedEmployees, e => Assert.NotNull(e.DeletedAt));
    }

    [Fact]
    public void DeleteRange_ShouldDeleteEntity_WhenEntityNotSupportsSoftDelete()
    {
        // Arrange
        var companies = new List<Company>
        {
            new Company
            {
                Name = "Company1"
            },
            new Company
            {
                Name = "Company2"
            }
        };

        _dbContext.Companies.AddRange(companies);
        _dbContext.SaveChanges();
        DetachAllEntities();

        var sut = new CompanyRepository(_dbContext);

        // Act
        sut.DeleteRange(companies);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        var remainingCompanies = _dbContext.Companies.ToList();
        Assert.Empty(remainingCompanies);
    }
}

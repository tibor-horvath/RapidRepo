using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Tests.Repositories.TestData;
using RapidRepo.Tests.Repositories.WriteRepository.TestData;

namespace RapidRepo.Tests.Repositories.WriteRepository;
public class DeleteTests : BaseWriteRepositoryTest
{
    [Fact]
    public void Delete_ShouldRemoveEntityFromDbContext()
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
            FirstName = "Jack",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
        };

        _dbContext.Employees.Add(employee1);
        _dbContext.Employees.Add(employee2);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        _sut.Delete(employee1);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Assert
        var deletedCompany = _dbContext.Employees.Find(employee1.Id);
        deletedCompany.Should().BeNull();
        var notDeletedCompany = _dbContext.Employees.Find(employee2.Id);
        notDeletedCompany.Should().NotBeNull();
    }

    [Fact]
    public void Delete_ShouldSoftDeleteEntity_WhenEntitySupportsSoftDelete()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Salary = 50000,
            StartDate = DateTime.UtcNow
        };

        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        _sut.Delete(employee);
        _dbContext.SaveChanges();

        // Assert
        var softDeletedEmployee = _dbContext.Employees.IgnoreQueryFilters().FirstOrDefault(e => e.Id == employee.Id);
        softDeletedEmployee.Should().NotBeNull();
        softDeletedEmployee!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Delete_ShouldDeleteEntity_WhenEntityNotSupportsSoftDelete()
    {
        // Arrange
        var company = new Company
        {
            Name = "Company"
        };

        _dbContext.Companies.Add(company);
        _dbContext.SaveChanges();
        DetachAllEntities();

        var sut = new WriteCompanyRepository(_dbContext);

        // Act
        sut.Delete(company);
        _dbContext.SaveChanges();

        // Assert
        var deletedCompany = _dbContext.Companies.FirstOrDefault(x => x.Id == company.Id);
        deletedCompany.Should().BeNull();
    }

    [Fact]
    public void Delete_ShouldNotThrow_WhenEntityIsNotTracked()
    {
        // Arrange
        var employee = new Employee
        {
            FirstName = "John",
            LastName = "Doe",
            DateOfBirth = new DateTime(1990, 1, 1),
            Salary = 50000,
            StartDate = DateTime.UtcNow
        };

        _dbContext.Employees.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        Action act = () => _sut.Delete(employee);

        // Assert
        act.Should().NotThrow();
    }
}

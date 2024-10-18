using AutoFixture;
using FluentAssertions;
using Repository.Tests.TestData;

namespace RapidRepo.Tests.Repositories.BaseRepository;
public class GetByIdAsyncTests : BaseRepositoryTest
{
    [Fact]
    public async Task GetByIdAsync_ShouldReturnEntityWithMatchingId()
    {
        // Arrange
        var employee = _fixture
            .Build<Employee>()
            .Without(e => e.Id)
            .Create();

        _sut.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetByIdAsync(employee.Id);

        // Assert
        result.Should().BeEquivalentTo(employee);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnEntityWithMatchingIdWithFirstName()
    {
        // Arrange
        var fistNameExpected = "TestFirstName";
        var testEmployeeId = 78;

        var employee = _fixture
            .Build<Employee>()
            .With(e => e.Id, testEmployeeId)
            .With(e => e.FirstName, fistNameExpected)
            .Create();

        var employees = _fixture.CreateMany<Employee>(4).ToList();
        employees.Add(employee);

        _sut.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetByIdAsync(id: testEmployeeId, selector: e => e.FirstName);

        // Assert
        result.Should().Be(fistNameExpected);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnEntityWithMatchingIdWithFirstAndLastName()
    {
        // Arrange
        var fistNameExpected = "TestFirstName";
        var lastNameExpected = "TestLastName";
        var testEmployeeId = 78;

        var employee = _fixture
            .Build<Employee>()
            .With(e => e.Id, testEmployeeId)
            .With(e => e.FirstName, fistNameExpected)
            .With(e => e.LastName, lastNameExpected)
            .Create();

        var employees = _fixture.CreateMany<Employee>(4).ToList();
        employees.Add(employee);

        _sut.Add(employee);
        _dbContext.SaveChanges();
        DetachAllEntities();

        // Act
        var result = await _sut.GetByIdAsync(id: testEmployeeId, selector: e => new { e.FirstName, e.LastName });

        // Assert
        result.Should().Be(new { FirstName = fistNameExpected, LastName = lastNameExpected });
    }
}

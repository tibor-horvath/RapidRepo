using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RapidRepo.EntityFrameworkCore.Tests.Repositories.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.ReadOnlyRepository;

/// <summary>
/// <see cref="EfReadOptions{TEntity}.SplitQuery"/> loads a collection include through a separate query. Only a
/// relational provider shows the difference, so these run on SQLite and count the commands sent.
/// </summary>
public sealed class SplitQueryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly List<string> _commands = [];
    private readonly TestDbContext _dbContext;
    private readonly EfRepository<Company, Guid> _sut;

    public SplitQueryTests()
    {
        _connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .LogTo(_commands.Add, [RelationalEventId.CommandExecuted])
            .Options;

        _dbContext = new TestDbContext(options);
        _dbContext.Database.EnsureCreated();
        _dbContext.Companies.Add(new Company
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            Employees = [new() { FirstName = "Ada", LastName = "L" }, new() { FirstName = "Bob", LastName = "M" }],
        });
        _dbContext.SaveChanges();
        _dbContext.ChangeTracker.Clear();
        _commands.Clear();

        _sut = new EfRepository<Company, Guid>(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void SplitQuery_LoadsTheCollectionThroughASecondCommand()
    {
        var companies = _sut.GetAll(options: new EfReadOptions<Company>
        {
            Include = q => q.Include(c => c.Employees),
            SplitQuery = true,
        }).ToList();

        companies.Should().ContainSingle().Which.Employees.Should().HaveCount(2);
        _commands.Should().HaveCount(2);
    }

    [Fact]
    public void WithoutSplitQuery_LoadsTheCollectionInOneCommand()
    {
        var companies = _sut.GetAll(options: new EfReadOptions<Company>
        {
            Include = q => q.Include(c => c.Employees),
        }).ToList();

        companies.Should().ContainSingle().Which.Employees.Should().HaveCount(2);
        _commands.Should().ContainSingle();
    }
}

using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RapidRepo.EntityFrameworkCore.Tests.Conformance;
using RapidRepo.Repositories;
using RapidRepo.Testing.Conformance;

namespace RapidRepo.EntityFrameworkCore.Tests.Repositories.ReadOnlyRepository;

/// <summary>
/// The SQL EF paging issues. The results alone cannot show it, because SQLite and InMemory happen to return rows in
/// key order anyway; a relational database is not obliged to, and SQL Server rejects OFFSET/FETCH without ORDER BY.
/// </summary>
public sealed class PagingOrderTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly List<string> _commands = [];
    private readonly ConformanceDbContext _dbContext;
    private readonly EfRepository<Widget, int> _sut;

    public PagingOrderTests()
    {
        _connection.Open();

        var options = new DbContextOptionsBuilder<ConformanceDbContext>()
            .UseSqlite(_connection)
            .LogTo(_commands.Add, [RelationalEventId.CommandExecuted])
            .Options;

        _dbContext = new ConformanceDbContext(options);
        _dbContext.Database.EnsureCreated();
        _dbContext.Widgets.AddRange(new Widget { Name = "B" }, new Widget { Name = "A" });
        _dbContext.SaveChanges();
        _commands.Clear();

        _sut = new EfRepository<Widget, int>(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void GetAllPaged_WithoutASortOrder_OrdersByTheKey()
    {
        _sut.GetAllPaged(pageSize: 1);

        PageCommand().Should().Contain("ORDER BY \"w\".\"Id\"");
    }

    [Fact]
    public async Task GetAllPagedAsync_Projection_WithoutASortOrder_OrdersByTheKey()
    {
        await _sut.GetAllPagedAsync(w => new { w.Name }, pageSize: 1);

        PageCommand().Should().Contain("ORDER BY \"w\".\"Id\"");
    }

    [Fact]
    public void GetAllPaged_WithASortOrder_OrdersByItInstead()
    {
        _sut.GetAllPaged(orderBy: SortOrder<Widget>.By(w => w.Name), pageSize: 1);

        PageCommand().Should().Contain("ORDER BY \"w\".\"Name\"").And.NotContain("ORDER BY \"w\".\"Id\"");
    }

    [Fact]
    public void GetAllPaged_WithAnEfOrderBy_UsesItInsteadOfTheKey()
    {
        _sut.GetAllPaged(pageSize: 1, options: new EfReadOptions<Widget> { OrderBy = q => q.OrderByDescending(w => w.Rank) });

        PageCommand().Should().Contain("ORDER BY \"w\".\"Rank\" DESC");
    }

    [Fact]
    public void GetAll_WithoutASortOrder_IsNotOrdered()
    {
        _sut.GetAll();

        _commands.Should().ContainSingle().Which.Should().NotContain("ORDER BY");
    }

    // The paging methods run a COUNT and then the page query; the page query is the one with LIMIT.
    private string PageCommand() => _commands.Single(c => c.Contains("LIMIT", StringComparison.Ordinal));
}

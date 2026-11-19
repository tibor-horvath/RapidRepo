using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Tests.Conformance;
using RapidRepo.Testing.Conformance;

namespace RapidRepo.EntityFrameworkCore.Tests.UnitOfWork;

/// <summary>
/// Strict mode on a real database: a commit without a user fails only when it would stamp a user field, and then
/// persists nothing.
/// </summary>
public sealed class RequireCurrentUserTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<ConformanceDbContext> _options;

    public RequireCurrentUserTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<ConformanceDbContext>().UseSqlite(_connection).Options;

        using var dbContext = new ConformanceDbContext(_options);
        dbContext.Database.EnsureCreated();
    }

    [Fact]
    public void CommitWithoutAUser_OfAnEntityWithUserFields_Throws_AndPersistsNothing()
    {
        using var dbContext = new ConformanceDbContext(_options);
        var uow = new StrictUnitOfWork(dbContext);
        new EfRepository<Widget, int>(dbContext).Add(new Widget { Name = "plain" });
        new EfRepository<Note, int>(dbContext).Add(new Note { Title = "audited" });

        var commit = () => uow.Commit();

        commit.Should().Throw<InvalidOperationException>().WithMessage("*RequireCurrentUser*");
        using var reader = new ConformanceDbContext(_options);
        reader.Widgets.Should().BeEmpty();
        reader.Notes.Should().BeEmpty();
    }

    [Fact]
    public async Task CommitWithoutAUser_OfEntitiesWithoutUserFields_Succeeds()
    {
        await using var dbContext = new ConformanceDbContext(_options);
        var uow = new StrictUnitOfWork(dbContext);
        new EfRepository<Widget, int>(dbContext).Add(new Widget { Name = "plain" });

        await uow.CommitAsync();

        await using var reader = new ConformanceDbContext(_options);
        reader.Widgets.Should().ContainSingle(w => w.Name == "plain");
    }

    [Fact]
    public void CommitWithAUser_InStrictMode_StampsIt()
    {
        var user = Guid.NewGuid();
        using var dbContext = new ConformanceDbContext(_options);
        var uow = new StrictUnitOfWork(dbContext);
        var note = new Note { Title = "audited" };
        new EfRepository<Note, int>(dbContext).Add(note);

        uow.Commit(user);

        using var reader = new ConformanceDbContext(_options);
        reader.Notes.Single().CreatedBy.Should().Be(user);
    }

    [Fact]
    public void RetryWithAUser_AfterAStrictModeFailure_CommitsEverything_WithThatUser()
    {
        var user = Guid.NewGuid();
        using var dbContext = new ConformanceDbContext(_options);
        var uow = new StrictUnitOfWork(dbContext);
        var widget = new Widget { Name = "plain" };
        var first = new Note { Title = "first" };
        var second = new Note { Title = "second" };
        new EfRepository<Widget, int>(dbContext).Add(widget);
        new EfRepository<Note, int>(dbContext).AddRange([first, second]);

        var commit = () => uow.Commit();
        commit.Should().Throw<InvalidOperationException>();

        uow.Commit(user);

        using var reader = new ConformanceDbContext(_options);
        reader.Widgets.Should().ContainSingle(w => w.Name == "plain");
        reader.Notes.Select(n => n.CreatedBy).Should().Equal(user, user);
        reader.Notes.Select(n => n.CreatedAt).Distinct().Should().ContainSingle()
            .Which.Should().NotBe(default);
    }

    public void Dispose() => _connection.Dispose();

    private sealed class StrictUnitOfWork(DbContext dbContext)
        : EfUnitOfWork<Guid>(dbContext, ConformanceDefaults.DefaultUser)
    {
        protected override bool RequireCurrentUser => true;
    }
}

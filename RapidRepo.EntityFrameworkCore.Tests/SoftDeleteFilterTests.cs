using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Extensions;
using RapidRepo.Repositories;

namespace RapidRepo.EntityFrameworkCore.Tests;

/// <summary>
/// The named soft-delete filter (EF Core 10): <see cref="ReadOptions.IncludeDeleted"/> bypasses soft delete alone
/// and leaves other filters, here a tenant filter, in force.
/// </summary>
public class SoftDeleteFilterTests : IDisposable
{
    private const int MyTenant = 1;
    private const int OtherTenant = 2;

    private static readonly ReadOptions IncludeDeleted = new() { IncludeDeleted = true };

    private readonly FilterContext _dbContext;
    private readonly NoteRepository _notes;
    private readonly LegacyRepository _legacies;

    public SoftDeleteFilterTests()
    {
        var options = new DbContextOptionsBuilder<FilterContext>()
            .UseInMemoryDatabase($"SoftDeleteFilterTests-{Guid.NewGuid()}")
            .Options;

        _dbContext = new FilterContext(options) { TenantId = MyTenant };
        _dbContext.Database.EnsureCreated();
        _notes = new NoteRepository(_dbContext);
        _legacies = new LegacyRepository(_dbContext);

        _dbContext.AddRange(
            new Note { Title = "mine", TenantId = MyTenant },
            new Note { Title = "mine, deleted", TenantId = MyTenant, DeletedAt = DateTime.UtcNow },
            new Note { Title = "theirs", TenantId = OtherTenant },
            new Note { Title = "theirs, deleted", TenantId = OtherTenant, DeletedAt = DateTime.UtcNow },
            new Legacy { Title = "visible" },
            new Legacy { Title = "hidden" },
            new Legacy { Title = "deleted", DeletedAt = DateTime.UtcNow });
        _dbContext.SaveChanges();
        _dbContext.ChangeTracker.Clear();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AddSoftDeleteQueryFilters_RegistersANamedFilter_NextToOtherNamedFilters()
    {
        var filters = _dbContext.Model.FindEntityType(typeof(Note))!.GetDeclaredQueryFilters();

        filters.Select(f => f.Key).Should().BeEquivalentTo([ModelBuilderExtensions.SoftDeleteFilterKey, "Tenant"]);
    }

    [Fact]
    public void AddSoftDeleteQueryFilters_KeepsAnAnonymousFilter_WhenTheEntityAlreadyHasOne()
    {
        var filters = _dbContext.Model.FindEntityType(typeof(Legacy))!.GetDeclaredQueryFilters();

        filters.Should().ContainSingle().Which.IsAnonymous.Should().BeTrue();
    }

    [Fact]
    public void Read_ByDefault_AppliesBothFilters()
    {
        _notes.GetAll().Select(n => n.Title).Should().BeEquivalentTo(["mine"]);
    }

    [Fact]
    public void Read_IncludeDeleted_BypassesSoftDeleteOnly()
    {
        _notes.GetAll(options: IncludeDeleted).Select(n => n.Title)
            .Should().BeEquivalentTo(["mine", "mine, deleted"]);
        _notes.Count(options: IncludeDeleted).Should().Be(2);
        _notes.Any(n => n.Title == "theirs, deleted", IncludeDeleted).Should().BeFalse();
    }

    [Fact]
    public void Read_IgnoreAllQueryFilters_BypassesEveryFilter()
    {
        _notes.GetAll(options: new EfReadOptions<Note> { IgnoreAllQueryFilters = true }).Should().HaveCount(4);
    }

    [Fact]
    public void Query_WithIncludeDeleted_KeepsTheTenantFilter()
    {
        _notes.Query(new EfReadOptions<Note> { IncludeDeleted = true }).Count().Should().Be(2);
    }

    [Fact]
    public void RestoreById_CannotReachAnotherTenantsDeletedRow()
    {
        var theirs = IdOf("theirs, deleted");

        _notes.RestoreById(theirs);
        _dbContext.SaveChanges();

        _dbContext.Notes.IgnoreQueryFilters().AsNoTracking().Single(n => n.Id == theirs).DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void RestoreById_RestoresOwnTenantsDeletedRow()
    {
        var mine = IdOf("mine, deleted");

        _notes.RestoreById(mine);
        _dbContext.SaveChanges();

        _notes.GetById(mine).Should().NotBeNull();
    }

    [Fact]
    public async Task HardDeleteByIdAsync_IncludeDeleted_CannotEraseAnotherTenantsRow()
    {
        var theirs = IdOf("theirs, deleted");

        await _notes.HardDeleteByIdAsync(theirs, includeDeleted: true);
        await _dbContext.SaveChangesAsync();

        _dbContext.Notes.IgnoreQueryFilters().Any(n => n.Id == theirs).Should().BeTrue();
    }

    [Fact]
    public void AnonymousFilter_IncludeDeleted_FallsBackToBypassingEveryFilter()
    {
        // As in 8.x, AddSoftDeleteQueryFilters replaced the existing anonymous filter, so "hidden" is visible.
        _legacies.GetAll().Select(l => l.Title).Should().BeEquivalentTo(["visible", "hidden"]);
        _legacies.GetAll(options: IncludeDeleted).Should().HaveCount(3);
    }

    private int IdOf(string title) =>
        _dbContext.Notes.IgnoreQueryFilters().AsNoTracking().Single(n => n.Title == title).Id;

    private sealed class Note : BaseEntity<int>, IDeletableEntity
    {
        public required string Title { get; set; }
        public int TenantId { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    private sealed class Legacy : BaseEntity<int>, IDeletableEntity
    {
        public required string Title { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    private sealed class FilterContext(DbContextOptions<FilterContext> options) : DbContext(options)
    {
        public int TenantId { get; init; }

        public DbSet<Note> Notes => Set<Note>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Note>();
            modelBuilder.Entity<Legacy>().HasQueryFilter(l => l.Title != "hidden");

            modelBuilder.AddSoftDeleteQueryFilters();

            modelBuilder.Entity<Note>().HasQueryFilter("Tenant", n => n.TenantId == TenantId);
        }
    }

    private sealed class NoteRepository(DbContext dbContext) : EfRepository<Note, int>(dbContext);

    private sealed class LegacyRepository(DbContext dbContext) : EfRepository<Legacy, int>(dbContext);
}

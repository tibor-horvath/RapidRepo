using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Concurrency;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Extensions;

namespace RapidRepo.EntityFrameworkCore.Tests;

/// <summary>
/// EF Core specifics of optimistic concurrency: the model convention, the guard against a missing token, and the
/// translation of <see cref="DbUpdateConcurrencyException"/>. The provider-neutral behaviour is covered by the
/// conformance suite.
/// </summary>
public class ConcurrencyTests
{
    [Fact]
    public void AddConcurrencyTokens_MarksTheVersionOfEveryVersionedEntity_AndNothingElse()
    {
        using var context = Create<TokenContext>();
        var model = context.Model;

        model.FindEntityType(typeof(Document))!.FindProperty(nameof(Document.Version))!.IsConcurrencyToken.Should().BeTrue();
        model.FindEntityType(typeof(SpecialDocument))!.FindProperty(nameof(Document.Version))!.IsConcurrencyToken.Should().BeTrue();
        model.FindEntityType(typeof(Plain))!.FindProperty(nameof(Plain.Version))!.IsConcurrencyToken.Should().BeFalse();
    }

    [Fact]
    public void AddConcurrencyTokens_MarksAVersionInheritedFromABaseTypeThatDoesNotOptIn()
    {
        using var context = Create<InheritedTokenContext>();
        context.Database.EnsureCreated();

        context.Model.FindEntityType(typeof(VersionedLeaf))!.FindProperty(nameof(VersionedLeaf.Version))!
            .IsConcurrencyToken.Should().BeTrue();

        var leaf = new VersionedLeaf();
        context.Add(leaf);
        new TestUow(context).Commit();
        leaf.Title = "Edited";
        context.Update(leaf);
        new TestUow(context).Commit();

        leaf.Version.Should().Be(1);
    }

    [Fact]
    public void AddConcurrencyTokens_YieldsToExplicitConfiguration()
    {
        using var context = Create<OptOutContext>();

        context.Model.FindEntityType(typeof(Document))!.FindProperty(nameof(Document.Version))!.IsConcurrencyToken.Should().BeFalse();
    }

    [Fact]
    public void Commit_OfAVersionedEntityWithoutAToken_Throws_AndWritesNothing()
    {
        using var context = Create<NoTokenContext>();
        var unitOfWork = new TestUow(context);
        context.Add(new Document { Title = "New" });

        var commit = () => unitOfWork.Commit();

        commit.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(ModelConfigurationBuilderExtensions.AddConcurrencyTokens)}*");
        context.ChangeTracker.Clear();
        context.Set<Document>().Count().Should().Be(0);
    }

    [Fact]
    public void Commit_OfAVersionedEntityMarkedWithConcurrencyCheck_Works()
    {
        using var context = Create<AttributeTokenContext>();
        var unitOfWork = new TestUow(context);
        var row = new AttributedDocument { Title = "New" };
        context.Add(row);
        unitOfWork.Commit();

        row.Title = "Edited";
        unitOfWork.Commit();

        row.Version.Should().Be(1);
    }

    [Fact]
    public void Conflict_KeepsEfCoresException_AsInnerException()
    {
        var name = Guid.NewGuid().ToString();
        using var first = Create<TokenContext>(name);
        using var second = Create<TokenContext>(name);
        first.Add(new Document { Id = 1, Title = "Original" });
        first.SaveChanges();
        var stale = second.Set<Document>().Single();

        var fresh = first.Set<Document>().Single();
        fresh.Title = "First";
        new TestUow(first).Commit();

        stale.Title = "Second";
        var commit = () => new TestUow(second).Commit();

        commit.Should().Throw<ConcurrencyException>()
            .WithInnerException<DbUpdateConcurrencyException>();
    }

    [Fact]
    public void ConflictOfANonVersionedEntity_StaysEfCoresException()
    {
        var name = Guid.NewGuid().ToString();
        using var first = Create<TokenContext>(name);
        using var second = Create<TokenContext>(name);
        first.Add(new Plain { Id = 1 });
        first.SaveChanges();
        var stale = second.Set<Plain>().Single();

        first.Remove(first.Set<Plain>().Single());
        new TestUow(first).Commit();

        stale.Version = 42;
        var commit = () => new TestUow(second).Commit();

        commit.Should().Throw<DbUpdateConcurrencyException>();
    }

    private static TContext Create<TContext>(string? name = null)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return (TContext)Activator.CreateInstance(typeof(TContext), options)!;
    }

    public class Document : BaseVersionedEntity<int>
    {
        public string Title { get; set; } = "";
    }

    public class SpecialDocument : Document;

    public class AttributedDocument : BaseEntity<int>, IVersionedEntity
    {
        public string Title { get; set; } = "";

        [System.ComponentModel.DataAnnotations.ConcurrencyCheck]
        public long Version { get; set; }
    }

    /// <summary>Has a property named Version but does not opt in.</summary>
    public class Plain : BaseEntity<int>
    {
        public long Version { get; set; }
    }

    /// <summary>A mapped base type that declares Version without opting in.</summary>
    public class UnversionedBase : BaseEntity<int>
    {
        public string Title { get; set; } = "";
        public long Version { get; set; }
    }

    /// <summary>Opts in below the type that declares Version.</summary>
    public class VersionedLeaf : UnversionedBase, IVersionedEntity;

    public sealed class InheritedTokenContext(DbContextOptions<InheritedTokenContext> options) : DbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
            configurationBuilder.AddConcurrencyTokens();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UnversionedBase>();
            modelBuilder.Entity<VersionedLeaf>();
        }
    }

    public sealed class TokenContext(DbContextOptions<TokenContext> options) : DbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
            configurationBuilder.AddConcurrencyTokens();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Document>();
            modelBuilder.Entity<SpecialDocument>();
            modelBuilder.Entity<Plain>();
        }
    }

    /// <summary>Opts out explicitly: configuration wins over the convention.</summary>
    public sealed class OptOutContext(DbContextOptions<OptOutContext> options) : DbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
            configurationBuilder.AddConcurrencyTokens();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<Document>().Property(d => d.Version).IsConcurrencyToken(false);
    }

    public sealed class NoTokenContext(DbContextOptions<NoTokenContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Document>();
    }

    public sealed class AttributeTokenContext(DbContextOptions<AttributeTokenContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<AttributedDocument>();
    }

    private sealed class TestUow(DbContext context) : EfUnitOfWork<Guid>(context, Guid.Empty);
}

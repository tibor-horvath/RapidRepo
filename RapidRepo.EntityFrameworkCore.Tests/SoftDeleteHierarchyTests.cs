using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Extensions;
using RapidRepo.Repositories;

namespace RapidRepo.EntityFrameworkCore.Tests;

/// <summary>
/// <see cref="ModelBuilderExtensions.AddSoftDeleteQueryFilters"/> on inheritance hierarchies: EF Core only allows
/// query filters on the root type.
/// </summary>
public class SoftDeleteHierarchyTests
{
    [Fact]
    public void DeletableRoot_FiltersSoftDeletedRowsOfDerivedTypes()
    {
        using var context = Create<DeletableRootContext>(o => new DeletableRootContext(o));
        context.AddRange(new Cat { Name = "kept" }, new Cat { Name = "gone", DeletedAt = DateTime.UtcNow });
        context.SaveChanges();
        context.ChangeTracker.Clear();

        var cats = new EfRepository<Cat, int>(context);

        cats.GetAll().Select(c => c.Name).Should().Equal("kept");
        cats.GetAll(options: new ReadOptions { IncludeDeleted = true }).Should().HaveCount(2);
    }

    [Fact]
    public void DeletableDerivedType_UnderARootThatIsNotDeletable_Throws()
    {
        using var context = Create<DeletableLeafContext>(o => new DeletableLeafContext(o));

        var act = () => context.Model;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*'{nameof(Dog)}'*'{nameof(Animal)}'*");
    }

    private static TContext Create<TContext>(Func<DbContextOptions<TContext>, TContext> factory)
        where TContext : DbContext =>
        factory(new DbContextOptionsBuilder<TContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private class Pet : BaseEntity<int>, IDeletableEntity
    {
        public required string Name { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    private sealed class Cat : Pet;

    private class Animal : BaseEntity<int>
    {
        public string? Name { get; set; }
    }

    private sealed class Dog : Animal, IDeletableEntity
    {
        public DateTime? DeletedAt { get; set; }
    }

    private sealed class DeletableRootContext(DbContextOptions<DeletableRootContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Pet>();
            modelBuilder.Entity<Cat>();
            modelBuilder.AddSoftDeleteQueryFilters();
        }
    }

    private sealed class DeletableLeafContext(DbContextOptions<DeletableLeafContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Animal>();
            modelBuilder.Entity<Dog>();
            modelBuilder.AddSoftDeleteQueryFilters();
        }
    }
}

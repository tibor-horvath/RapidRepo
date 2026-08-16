using FluentAssertions;
using RapidRepo.Extensions.DependencyInjection.Internal;
using RapidRepo.Extensions.DependencyInjection.Tests.TestData;

namespace RapidRepo.Extensions.DependencyInjection.Tests;

/// <summary>
/// Entity discovery drives which generic repositories <c>UseDbContext</c> registers, so it is worth
/// pinning directly rather than only through what happens to resolve.
/// </summary>
public class DbContextRegistrarTests
{
    [Fact]
    public void DiscoverEntities_FindsPublicDbSets()
    {
        var entities = DbContextRegistrar.DiscoverEntities(typeof(DiscoveryDbContext)).ToList();

        entities.Should().Contain((typeof(Widget), typeof(int)));
    }

    /// <summary>
    /// EF Core reflects over runtime properties and maps non-public <c>DbSet</c>s, so a context declaring one
    /// gets the entity in its model. Discovery has to agree, or that entity silently gets no repository.
    /// </summary>
    [Fact]
    public void DiscoverEntities_FindsNonPublicDbSets()
    {
        var entities = DbContextRegistrar.DiscoverEntities(typeof(DiscoveryDbContext)).ToList();

        entities.Should().Contain((typeof(Gadget), typeof(Guid)));
    }

    [Fact]
    public void DiscoverEntities_ResolvesKeyTypeFromABaseClass()
    {
        var entities = DbContextRegistrar.DiscoverEntities(typeof(DiscoveryDbContext)).ToList();

        entities.Should().Contain((typeof(PremiumWidget), typeof(int)));
    }

    [Fact]
    public void DiscoverEntities_SkipsTypesThatAreNotBaseEntities()
    {
        var entities = DbContextRegistrar.DiscoverEntities(typeof(DiscoveryDbContext)).ToList();

        entities.Should().NotContain(e => e.EntityType == typeof(LegacyRecord));
    }

    [Fact]
    public void DiscoverEntities_ContextWithNoDbSets_ReturnsEmpty()
    {
        var entities = DbContextRegistrar.DiscoverEntities(typeof(Microsoft.EntityFrameworkCore.DbContext));

        entities.Should().BeEmpty();
    }
}

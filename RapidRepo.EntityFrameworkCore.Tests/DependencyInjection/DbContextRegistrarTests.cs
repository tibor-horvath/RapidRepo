using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RapidRepo.EntityFrameworkCore.Internal;
using RapidRepo.EntityFrameworkCore.Tests.DependencyInjection.TestData;

namespace RapidRepo.EntityFrameworkCore.Tests.DependencyInjection;

/// <summary>
/// Entity discovery drives which generic repositories <c>UseEntityFrameworkCore&lt;TContext&gt;()</c> registers, so it is worth
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
    public void DiscoverEntities_SkipsStaticAndIndexerProperties()
    {
        var entities = DbContextRegistrar.DiscoverEntities(typeof(OddPropertiesDbContext)).ToList();

        entities.Should().Equal((typeof(Widget), typeof(int)));
    }

    [Fact]
    public void RegisterForwarder_KeepsADbContextRegistrationTheApplicationMadeItself()
    {
        var services = new ServiceCollection();
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<DiscoveryDbContext>());

        DbContextRegistrar.RegisterForwarder(services, typeof(DiscoveryDbContext), [typeof(NeedsBaseDbContext)]);

        services.Where(d => d.ServiceType == typeof(DbContext)).Should().ContainSingle();
    }

    [Fact]
    public void RegisterForwarder_ForTheSameContextTwice_RegistersOnce()
    {
        var services = new ServiceCollection();

        DbContextRegistrar.RegisterForwarder(services, typeof(DiscoveryDbContext), [typeof(NeedsBaseDbContext)]);
        DbContextRegistrar.RegisterForwarder(services, typeof(DiscoveryDbContext), [typeof(NeedsBaseDbContext)]);

        services.Where(d => d.ServiceType == typeof(DbContext)).Should().ContainSingle();
    }

    private sealed class NeedsBaseDbContext(DbContext dbContext)
    {
        public DbContext DbContext { get; } = dbContext;
    }

    private sealed class OddPropertiesDbContext(DbContextOptions<OddPropertiesDbContext> options) : DbContext(options)
    {
        public static DbSet<Gadget>? StaticGadgets { get; set; }

        public DbSet<Widget> Widgets => Set<Widget>();

        public DbSet<PremiumWidget> this[int index] => Set<PremiumWidget>();
    }

    [Fact]
    public void DiscoverEntities_ContextWithNoDbSets_ReturnsEmpty()
    {
        var entities = DbContextRegistrar.DiscoverEntities(typeof(Microsoft.EntityFrameworkCore.DbContext));

        entities.Should().BeEmpty();
    }
}

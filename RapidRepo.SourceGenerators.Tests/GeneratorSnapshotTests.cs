using System.Reflection;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.CodeAnalysis;

namespace RapidRepo.SourceGenerators.Tests;

/// <summary>
/// Pins the exact generated source for DbContext-based usage, so changes to the generator cannot silently alter the
/// output of existing <c>[GenerateUnitOfWork(typeof(SomeDbContext))]</c> declarations. The only change from 8.0.0 is
/// a <c>using System;</c> where a key type such as <c>Guid</c> needs it.
/// </summary>
/// <remarks>
/// The expected output lives in <c>Snapshots/*.verified.txt</c>. On a mismatch the actual output is written next to
/// it as <c>*.received.txt</c>; review it, and if the change is intended, replace the verified file with it.
/// Line endings are normalised, since the generator emits the platform's newline.
/// </remarks>
public class GeneratorSnapshotTests
{
    private const string DbContextSource = """
        using Microsoft.EntityFrameworkCore;
        using RapidRepo.Attributes;
        using RapidRepo.Entities;
        using RapidRepo.Repositories.Interfaces;
        using RapidRepo.UnitOfWork;

        namespace Shop.Catalog
        {
            public readonly record struct Sku(string Value);

            public class Product : BaseEntity<Sku> { }

            public class Category : BaseEntity<int> { }
        }

        namespace Shop.Data
        {
            using Shop.Catalog;

            public class Customer : BaseEntity<System.Guid> { }

            public class Order : BaseEntity<long>
            {
                public class Line : BaseEntity<long> { }
            }

            public class ShopDbContext : DbContext
            {
                public DbSet<Product> Products { get; set; } = null!;
                public DbSet<Category> Categories { get; set; } = null!;
                public DbSet<Customer> Customers { get; set; } = null!;
            }

            public class SalesDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;
                public DbSet<Order.Line> OrderLines { get; set; } = null!;
            }
        }

        namespace Shop.Data.Repositories
        {
            using Shop.Data;

            public interface ICustomerRepository : IRepository<Customer, System.Guid> { }

            [UnitOfWorkProperty("Bookings")]
            public interface IOrderRepository : IReadOnlyRepository<Order, long> { }
        }

        namespace Shop
        {
            using Shop.Data;

            public interface IShopUnitOfWork : IUnitOfWork<System.Guid> { }

            [GenerateUnitOfWork(typeof(ShopDbContext), typeof(SalesDbContext))]
            public partial class ShopUnitOfWork : UnitOfWork<System.Guid>, IShopUnitOfWork
            {
                public ShopUnitOfWork(ShopDbContext db, System.IServiceProvider sp) : base(db, System.Guid.Empty, sp) { }
            }

            [GenerateUnitOfWork(typeof(ShopDbContext), RepositoriesInterfaceName = "ICatalogRepositories",
                IncludeNamespaces = ["Shop.Catalog"])]
            public partial class CatalogUnitOfWork : UnitOfWork<System.Guid>
            {
                public CatalogUnitOfWork(ShopDbContext db) : base(db, System.Guid.Empty) { }
            }
        }
        """;

    [Fact]
    public void DbContextSources_GenerateTheVerifiedOutput()
    {
        var sources = RunGenerator(DbContextSource);

        sources.Should().HaveCount(4);

        // Report every file that differs, and write all of them, rather than stopping at the first.
        using var scope = new AssertionScope();
        foreach (var (hintName, source) in sources)
            MatchSnapshot(hintName, source);
    }

    private static IReadOnlyList<(string HintName, string Source)> RunGenerator(string source)
    {
        var (diagnostics, sources) = GeneratorHarness.Run(GeneratorHarness.Compile(source));

        diagnostics.Should().BeEmpty();

        return sources.OrderBy(s => s.HintName, StringComparer.Ordinal).ToList();
    }

    // The project directory is embedded at build time (see the .csproj); [CallerFilePath] would give a /_/ path under
    // deterministic CI builds.
    private static readonly string SnapshotDirectory = Path.Combine(
        typeof(GeneratorSnapshotTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "ProjectDirectory").Value!,
        "Snapshots");

    private static void MatchSnapshot(string hintName, string actual)
    {
        var directory = SnapshotDirectory;
        var stem = Path.Combine(directory, Path.GetFileNameWithoutExtension(hintName));
        var verifiedPath = stem + ".verified.txt";
        var receivedPath = stem + ".received.txt";

        var normalised = Normalise(actual);
        var verified = File.Exists(verifiedPath) ? Normalise(File.ReadAllText(verifiedPath)) : null;

        if (verified == normalised)
        {
            File.Delete(receivedPath);
            return;
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(receivedPath, normalised);

        normalised.Should().Be(verified, $"the generated '{hintName}' must match '{verifiedPath}' (actual output written to '{receivedPath}')");
    }

    private static string Normalise(string text) => text.Replace("\r\n", "\n");
}

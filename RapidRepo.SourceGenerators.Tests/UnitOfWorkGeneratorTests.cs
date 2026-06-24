using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RapidRepo.SourceGenerators;

namespace RapidRepo.SourceGenerators.Tests;

public class UnitOfWorkGeneratorTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static (ImmutableArray<Diagnostic> Diagnostics, IReadOnlyList<(string HintName, string Source)> Sources)
        RunGenerator(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var generator = new UnitOfWorkGenerator();
        var driver    = CSharpGeneratorDriver.Create(generator);
        driver        = (CSharpGeneratorDriver)driver.RunGenerators(compilation);

        var result  = driver.GetRunResult().Results[0];
        var sources = result.GeneratedSources
            .Select(s => (s.HintName, s.SourceText.ToString()))
            .ToList();

        return (result.Diagnostics, sources);
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        // Core runtime
        yield return MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location);

        // System assemblies (needed for full type resolution)
        var systemRuntime = Assembly.Load("System.Runtime");
        yield return MetadataReference.CreateFromFile(systemRuntime.Location);

        // RapidRepo (attributes + base types)
        yield return MetadataReference.CreateFromFile(typeof(RapidRepo.UnitOfWork.UnitOfWork<>).Assembly.Location);

        // EF Core
        yield return MetadataReference.CreateFromFile(typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly.Location);
    }

    private static string FindSource(IReadOnlyList<(string HintName, string Source)> sources, string hintNameContains)
        => sources.First(s => s.HintName.Contains(hintNameContains)).Source;

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public void DbSet_Properties_Generate_Generic_Repository_Properties()
    {
        var source = """
            using RapidRepo.Attributes;
            using RapidRepo.UnitOfWork;
            using RapidRepo.Entities;
            using Microsoft.EntityFrameworkCore;

            namespace MyApp.Data;

            public class User : BaseEntity<System.Guid> { }
            public class Product : BaseEntity<int> { }

            public class AppDbContext : DbContext
            {
                public DbSet<User> Users { get; set; } = null!;
                public DbSet<Product> Products { get; set; } = null!;
            }

            public interface IAppUoW : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

            [GenerateUnitOfWork(typeof(AppDbContext))]
            public partial class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoW
            {
                public AppUnitOfWork(AppDbContext db, System.IServiceProvider sp) : base(db, System.Guid.Empty, sp) { }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        diagnostics.Should().BeEmpty();
        sources.Should().HaveCount(2);

        var iface = FindSource(sources, "IAppUnitOfWorkRepositories");
        iface.Should().Contain("IRepository<User, Guid> Users { get; }");
        iface.Should().Contain("IRepository<Product, int> Products { get; }");

        var cls = FindSource(sources, "AppUnitOfWork.Repositories");
        cls.Should().Contain("GetRepository<User, Guid>()");
        cls.Should().Contain("GetRepository<Product, int>()");
    }

    [Fact]
    public void DbSet_Nested_Entity_Type_Uses_Full_Type_Display()
    {
        var source = """
            using RapidRepo.Attributes;
            using RapidRepo.UnitOfWork;
            using RapidRepo.Entities;
            using Microsoft.EntityFrameworkCore;

            namespace MyApp.Data;

            public class Catalog
            {
                public class Product : BaseEntity<int> { }
            }

            public class AppDbContext : DbContext
            {
                public DbSet<Catalog.Product> Products { get; set; } = null!;
            }

            public interface IAppUoW : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

            [GenerateUnitOfWork(typeof(AppDbContext))]
            public partial class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoW
            {
                public AppUnitOfWork(AppDbContext db, System.IServiceProvider sp) : base(db, System.Guid.Empty, sp) { }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        diagnostics.Should().BeEmpty();

        var iface = FindSource(sources, "IAppUnitOfWorkRepositories");
        iface.Should().Contain("IRepository<Catalog.Product, int> Products { get; }");

        var cls = FindSource(sources, "AppUnitOfWork.Repositories");
        cls.Should().Contain("GetRepository<Catalog.Product, int>()");
    }

    [Fact]
    public void Custom_Repo_Overrides_DbSet_Entry_For_Same_Entity()
    {
        var source = """
            using RapidRepo.Attributes;
            using RapidRepo.UnitOfWork;
            using RapidRepo.Entities;
            using RapidRepo.Repositories.Interfaces;
            using Microsoft.EntityFrameworkCore;

            namespace MyApp.Data;

            public class Match : BaseEntity<System.Guid> { }

            public class AppDbContext : DbContext
            {
                public DbSet<Match> Matches { get; set; } = null!;
            }

            public interface IMatchRepository : IRepository<Match, System.Guid> { }

            public interface IAppUoW : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

            [GenerateUnitOfWork(typeof(AppDbContext))]
            public partial class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoW
            {
                public AppUnitOfWork(AppDbContext db, System.IServiceProvider sp) : base(db, System.Guid.Empty, sp) { }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        diagnostics.Should().BeEmpty();

        var iface = FindSource(sources, "IAppUnitOfWorkRepositories");
        iface.Should().Contain("IMatchRepository Matches { get; }");
        iface.Should().NotContain("IRepository<Match");

        var cls = FindSource(sources, "AppUnitOfWork.Repositories");
        cls.Should().Contain("ResolveRepository<IMatchRepository>()");
        cls.Should().NotContain("GetRepository<Match");
    }

    [Fact]
    public void UnitOfWorkProperty_Attribute_Overrides_Derived_Name()
    {
        var source = """
            using RapidRepo.Attributes;
            using RapidRepo.UnitOfWork;
            using RapidRepo.Entities;
            using RapidRepo.Repositories.Interfaces;
            using Microsoft.EntityFrameworkCore;

            namespace MyApp.Data;

            public class Person : BaseEntity<System.Guid> { }

            public class AppDbContext : DbContext
            {
                public DbSet<Person> Persons { get; set; } = null!;
            }

            [UnitOfWorkProperty("People")]
            public interface IPersonRepository : IRepository<Person, System.Guid> { }

            public interface IAppUoW : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

            [GenerateUnitOfWork(typeof(AppDbContext))]
            public partial class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoW
            {
                public AppUnitOfWork(AppDbContext db, System.IServiceProvider sp) : base(db, System.Guid.Empty, sp) { }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        diagnostics.Should().BeEmpty();

        var iface = FindSource(sources, "IAppUnitOfWorkRepositories");
        iface.Should().Contain("IPersonRepository People { get; }");
        iface.Should().NotContain("Persons");
    }

    [Fact]
    public void NotPartial_Emits_RRUOW003_Error()
    {
        var source = """
            using RapidRepo.Attributes;
            using RapidRepo.UnitOfWork;
            using Microsoft.EntityFrameworkCore;

            namespace MyApp.Data;

            public class AppDbContext : DbContext { }

            public interface IAppUoW : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

            [GenerateUnitOfWork(typeof(AppDbContext))]
            public class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoW
            {
                public AppUnitOfWork(AppDbContext db) : base(db, System.Guid.Empty) { }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        diagnostics.Should().ContainSingle(d => d.Id == "RRUOW003");
        sources.Should().BeEmpty();
    }

    [Fact]
    public void NonDbContext_Type_Emits_RRUOW004_Error()
    {
        var source = """
            using RapidRepo.Attributes;
            using RapidRepo.UnitOfWork;

            namespace MyApp.Data;

            public class NotADbContext { }

            public interface IAppUoW : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

            [GenerateUnitOfWork(typeof(NotADbContext))]
            public partial class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoW
            {
                public AppUnitOfWork(System.IServiceProvider sp) : base(null!, System.Guid.Empty, sp) { }
            }
            """;

        var (diagnostics, _) = RunGenerator(source);

        diagnostics.Should().ContainSingle(d => d.Id == "RRUOW004");
    }

    [Fact]
    public void Entity_Without_BaseEntity_Emits_RRUOW001_Warning_And_Skips_Property()
    {
        var source = """
            using RapidRepo.Attributes;
            using RapidRepo.UnitOfWork;
            using RapidRepo.Entities;
            using Microsoft.EntityFrameworkCore;

            namespace MyApp.Data;

            public class GoodEntity : BaseEntity<System.Guid> { }
            public class BadEntity { }  // no BaseEntity

            public class AppDbContext : DbContext
            {
                public DbSet<GoodEntity> GoodEntities { get; set; } = null!;
                public DbSet<BadEntity> BadEntities { get; set; } = null!;
            }

            public interface IAppUoW : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

            [GenerateUnitOfWork(typeof(AppDbContext))]
            public partial class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoW
            {
                public AppUnitOfWork(AppDbContext db, System.IServiceProvider sp) : base(db, System.Guid.Empty, sp) { }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        diagnostics.Should().ContainSingle(d => d.Id == "RRUOW001" && d.Severity == DiagnosticSeverity.Warning);
        sources.Should().HaveCount(2);

        var iface = FindSource(sources, "IAppUnitOfWorkRepositories");
        iface.Should().Contain("GoodEntities");
        iface.Should().NotContain("BadEntities");
    }

    [Fact]
    public void ExcludeNamespaces_Filters_Custom_Repos()
    {
        var source = """
            using RapidRepo.Attributes;
            using RapidRepo.UnitOfWork;
            using RapidRepo.Entities;
            using RapidRepo.Repositories.Interfaces;
            using Microsoft.EntityFrameworkCore;

            namespace MyApp.Data.Included
            {
                public class Widget : BaseEntity<int> { }
                public interface IWidgetRepository : IRepository<Widget, int> { }
            }

            namespace MyApp.Data.Excluded
            {
                public class Gadget : BaseEntity<System.Guid> { }
                public interface IGadgetRepository : IRepository<Gadget, System.Guid> { }
            }

            namespace MyApp.Data
            {
                public class AppDbContext : Microsoft.EntityFrameworkCore.DbContext { }

                public interface IAppUoW : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

                [GenerateUnitOfWork(typeof(AppDbContext), ExcludeNamespaces = new[] { "MyApp.Data.Excluded" })]
                public partial class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoW
                {
                    public AppUnitOfWork(System.IServiceProvider sp) : base(null!, System.Guid.Empty, sp) { }
                }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        sources.Should().HaveCount(2);
        var iface = FindSource(sources, "IAppUnitOfWorkRepositories");
        iface.Should().Contain("IWidgetRepository");
        iface.Should().NotContain("IGadgetRepository");
    }

    [Fact]
    public void ExcludeNamespaces_Does_Not_Match_Sibling_Namespace_Prefixes()
    {
        var source = """
            using RapidRepo.Attributes;
            using RapidRepo.UnitOfWork;
            using RapidRepo.Entities;
            using RapidRepo.Repositories.Interfaces;
            using Microsoft.EntityFrameworkCore;

            namespace MyApp.Data.ExcludedExtra
            {
                public class Bonus : BaseEntity<int> { }
                public interface IBonusRepository : IRepository<Bonus, int> { }
            }

            namespace MyApp.Data.Excluded
            {
                public class Gadget : BaseEntity<System.Guid> { }
                public interface IGadgetRepository : IRepository<Gadget, System.Guid> { }
            }

            namespace MyApp.Data
            {
                public class AppDbContext : Microsoft.EntityFrameworkCore.DbContext { }

                public interface IAppUoW : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

                [GenerateUnitOfWork(typeof(AppDbContext), ExcludeNamespaces = new[] { "MyApp.Data.Excluded" })]
                public partial class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoW
                {
                    public AppUnitOfWork(System.IServiceProvider sp) : base(null!, System.Guid.Empty, sp) { }
                }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var iface = FindSource(sources, "IAppUnitOfWorkRepositories");
        iface.Should().Contain("IBonusRepository");
        iface.Should().NotContain("IGadgetRepository");
    }

    [Fact]
    public void Generated_Interface_Name_Can_Be_Overridden()
    {
        var source = """
            using RapidRepo.Attributes;
            using RapidRepo.UnitOfWork;
            using RapidRepo.Entities;
            using Microsoft.EntityFrameworkCore;

            namespace MyApp.Data;

            public class Item : BaseEntity<int> { }

            public class AppDbContext : DbContext
            {
                public DbSet<Item> Items { get; set; } = null!;
            }

            public interface IAppUoW : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

            [GenerateUnitOfWork(typeof(AppDbContext), RepositoriesInterfaceName = "ICustomName")]
            public partial class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoW
            {
                public AppUnitOfWork(AppDbContext db, System.IServiceProvider sp) : base(db, System.Guid.Empty, sp) { }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        diagnostics.Should().BeEmpty();
        sources.Should().Contain(s => s.HintName.Contains("ICustomName"));
    }

    [Fact]
    public void Duplicate_DbSet_Property_Name_Across_DbContexts_Emits_RRUOW002_Error()
    {
        var source = """
            using RapidRepo.Attributes;
            using RapidRepo.UnitOfWork;
            using RapidRepo.Entities;
            using Microsoft.EntityFrameworkCore;

            namespace MyApp.Data;

            public class User : BaseEntity<System.Guid> { }
            public class LegacyUser : BaseEntity<System.Guid> { }

            public class AppDbContext : DbContext
            {
                public DbSet<User> Users { get; set; } = null!;
            }

            public class LegacyDbContext : DbContext
            {
                public DbSet<LegacyUser> Users { get; set; } = null!;
            }

            public interface IAppUoW : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

            [GenerateUnitOfWork(typeof(AppDbContext), typeof(LegacyDbContext))]
            public partial class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoW
            {
                public AppUnitOfWork(AppDbContext db, System.IServiceProvider sp) : base(db, System.Guid.Empty, sp) { }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        diagnostics.Should().ContainSingle(d => d.Id == "RRUOW002" && d.Severity == DiagnosticSeverity.Error);

        var iface = FindSource(sources, "IAppUnitOfWorkRepositories");
        iface.Should().Contain("Users { get; }");
        iface.Split("Users { get; }", StringSplitOptions.None).Should().HaveCount(2);
    }

    [Fact]
    public void Generated_Files_Contain_Correct_Namespace()
    {
        var source = """
            using RapidRepo.Attributes;
            using RapidRepo.UnitOfWork;
            using RapidRepo.Entities;
            using Microsoft.EntityFrameworkCore;

            namespace Company.Project.Data;

            public class Order : BaseEntity<int> { }

            public class AppDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;
            }

            public interface IAppUoW : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

            [GenerateUnitOfWork(typeof(AppDbContext))]
            public partial class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoW
            {
                public AppUnitOfWork(AppDbContext db, System.IServiceProvider sp) : base(db, System.Guid.Empty, sp) { }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        diagnostics.Should().BeEmpty();

        var iface = FindSource(sources, "IAppUnitOfWorkRepositories");
        iface.Should().Contain("namespace Company.Project.Data;");

        var cls = FindSource(sources, "AppUnitOfWork.Repositories");
        cls.Should().Contain("namespace Company.Project.Data;");
    }

    [Fact]
    public void Hint_Names_Are_Namespace_Qualified_When_Class_Names_Collide()
    {
        var source = """
            using RapidRepo.Attributes;
            using RapidRepo.UnitOfWork;
            using RapidRepo.Entities;
            using Microsoft.EntityFrameworkCore;

            namespace Company.A.Data
            {
                public class ItemA : BaseEntity<int> { }

                public class AppDbContextA : DbContext
                {
                    public DbSet<ItemA> Items { get; set; } = null!;
                }

                public interface IAppUoWA : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

                [GenerateUnitOfWork(typeof(AppDbContextA))]
                public partial class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoWA
                {
                    public AppUnitOfWork(AppDbContextA db, System.IServiceProvider sp) : base(db, System.Guid.Empty, sp) { }
                }
            }

            namespace Company.B.Data
            {
                public class ItemB : BaseEntity<int> { }

                public class AppDbContextB : DbContext
                {
                    public DbSet<ItemB> Items { get; set; } = null!;
                }

                public interface IAppUoWB : RapidRepo.UnitOfWork.IUnitOfWork<System.Guid> { }

                [GenerateUnitOfWork(typeof(AppDbContextB))]
                public partial class AppUnitOfWork : UnitOfWork<System.Guid>, IAppUoWB
                {
                    public AppUnitOfWork(AppDbContextB db, System.IServiceProvider sp) : base(db, System.Guid.Empty, sp) { }
                }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        diagnostics.Should().BeEmpty();
        sources.Should().HaveCount(4);
        sources.Should().Contain(s => s.HintName == "Company.A.Data.IAppUnitOfWorkRepositories.g.cs");
        sources.Should().Contain(s => s.HintName == "Company.A.Data.AppUnitOfWork.Repositories.g.cs");
        sources.Should().Contain(s => s.HintName == "Company.B.Data.IAppUnitOfWorkRepositories.g.cs");
        sources.Should().Contain(s => s.HintName == "Company.B.Data.AppUnitOfWork.Repositories.g.cs");
    }
}

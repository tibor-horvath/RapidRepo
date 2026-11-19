using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RapidRepo.SourceGenerators.Tests;

/// <summary>
/// Entity types passed to <c>[GenerateUnitOfWork]</c> directly, the way a provider without a <c>DbContext</c> (for
/// example Dapper) uses the generator, alone and mixed with DbContext sources.
/// </summary>
public class EntitySourceTests
{
    private const string Entities = """
        namespace Shop.Catalog
        {
            public class Product : RapidRepo.Entities.BaseEntity<int> { }
            public class Category : RapidRepo.Entities.BaseEntity<int> { }
            public class Item : RapidRepo.Entities.BaseEntity<System.Guid> { }
            public class Box : RapidRepo.Entities.BaseEntity<long> { }
        }
        """;

    [Fact]
    public void EntitySources_GenerateGenericRepositoryProperties_WithoutEfCore()
    {
        var source = Entities + """
            namespace Shop
            {
                using RapidRepo.Attributes;
                using Shop.Catalog;

                [GenerateUnitOfWork(typeof(Product), typeof(Category), typeof(Item), typeof(Box))]
                public partial class ShopUnitOfWork : RapidRepo.UnitOfWork.UnitOfWorkBase<System.Guid>
                {
                    public ShopUnitOfWork() : base(System.Guid.Empty) { }
                }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source, includeEfCore: false);

        diagnostics.Should().BeEmpty();

        var iface = FindSource(sources, "IShopUnitOfWorkRepositories");
        iface.Should().Contain("IRepository<Product, int> Products { get; }");
        iface.Should().Contain("IRepository<Category, int> Categories { get; }");
        iface.Should().Contain("IRepository<Item, Guid> Items { get; }", "only interface names lose a leading 'I'");
        iface.Should().Contain("IRepository<Box, long> Boxes { get; }");
        iface.Should().Contain("using Shop.Catalog;");

        var cls = FindSource(sources, "ShopUnitOfWork.Repositories");
        cls.Should().Contain("public IRepository<Product, int> Products => GetRepository<Product, int>();");
        cls.Should().Contain("public IRepository<Item, Guid> Items => GetRepository<Item, Guid>();");
    }

    [Fact]
    public void EntitySources_GeneratedCode_CompilesAgainstTheNeutralUnitOfWorkBase()
    {
        var source = Entities + """
            namespace Shop
            {
                using RapidRepo.Attributes;
                using RapidRepo.Entities;
                using RapidRepo.Repositories.Interfaces;
                using Shop.Catalog;
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                [GenerateUnitOfWork(typeof(Product), typeof(Item))]
                public partial class ShopUnitOfWork : RapidRepo.UnitOfWork.UnitOfWorkBase<Guid>
                {
                    public ShopUnitOfWork() : base(Guid.Empty) { }

                    protected override IRepository<TEntity, TKey> CreateRepository<TEntity, TKey>() =>
                        throw new NotSupportedException();

                    protected override void CommitCore(Guid? userId) { }

                    protected override Task CommitCoreAsync(Guid? userId, CancellationToken cancellationToken) =>
                        Task.CompletedTask;
                }
            }
            """;

        var compilation = CreateCompilation(source, includeEfCore: false);
        CSharpGeneratorDriver.Create(new UnitOfWorkGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        generatorDiagnostics.Should().BeEmpty();
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the generated properties only use members of the provider-neutral base");
    }

    [Fact]
    public void MixedSources_DbContextFirst_KeepsTheDbSetName_AndAddsTheOtherEntities()
    {
        var source = Entities + """
            namespace Shop
            {
                using Microsoft.EntityFrameworkCore;
                using RapidRepo.Attributes;
                using Shop.Catalog;

                public class CatalogDbContext : DbContext
                {
                    public DbSet<Product> CatalogProducts { get; set; } = null!;
                }

                [GenerateUnitOfWork(typeof(CatalogDbContext), typeof(Product), typeof(Category))]
                public partial class ShopUnitOfWork : RapidRepo.UnitOfWork.UnitOfWorkBase<System.Guid>
                {
                    public ShopUnitOfWork() : base(System.Guid.Empty) { }
                }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        diagnostics.Should().BeEmpty();

        var iface = FindSource(sources, "IShopUnitOfWorkRepositories");
        iface.Should().Contain("IRepository<Product, int> CatalogProducts { get; }");
        iface.Should().NotContain(" Products { get; }", "an entity gets one property, from the first source that supplies it");
        iface.Should().Contain("IRepository<Category, int> Categories { get; }");
    }

    [Fact]
    public void EntitySource_IsReplacedByACustomRepositoryInterface_ForTheSameEntity()
    {
        var source = Entities + """
            namespace Shop
            {
                using RapidRepo.Attributes;
                using RapidRepo.Repositories.Interfaces;
                using Shop.Catalog;

                public interface IProductRepository : IRepository<Product, int> { }

                [GenerateUnitOfWork(typeof(Product), typeof(Category))]
                public partial class ShopUnitOfWork : RapidRepo.UnitOfWork.UnitOfWorkBase<System.Guid>
                {
                    public ShopUnitOfWork() : base(System.Guid.Empty) { }
                }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source, includeEfCore: false);

        diagnostics.Should().BeEmpty();

        var cls = FindSource(sources, "ShopUnitOfWork.Repositories");
        cls.Should().Contain("public IProductRepository Products => ResolveRepository<IProductRepository>();");
        cls.Should().Contain("public IRepository<Category, int> Categories => GetRepository<Category, int>();");
    }

    [Fact]
    public void EntitySources_WithTheSamePluralName_EmitRRUOW002()
    {
        var source = """
            namespace Shop.Sales { public class Order : RapidRepo.Entities.BaseEntity<int> { } }
            namespace Shop.Archive { public class Order : RapidRepo.Entities.BaseEntity<int> { } }

            namespace Shop
            {
                using RapidRepo.Attributes;

                [GenerateUnitOfWork(typeof(Shop.Sales.Order), typeof(Shop.Archive.Order))]
                public partial class ShopUnitOfWork : RapidRepo.UnitOfWork.UnitOfWorkBase<System.Guid>
                {
                    public ShopUnitOfWork() : base(System.Guid.Empty) { }
                }
            }
            """;

        var (diagnostics, _) = RunGenerator(source, includeEfCore: false);

        diagnostics.Should().ContainSingle(d => d.Id == "RRUOW002")
            .Which.GetMessage().Should().Contain("'Orders'");
    }

    [Theory]
    [InlineData("typeof(string)", "String")]
    [InlineData("typeof(Shop.Generic<>)", "Generic")]
    [InlineData("typeof(Shop.NotAnEntity)", "NotAnEntity")]
    public void TypeThatIsNeitherADbContextNorAnEntity_EmitsRRUOW004(string typeofExpression, string typeName)
    {
        var source = $$"""
            namespace Shop
            {
                using RapidRepo.Attributes;

                public class NotAnEntity { }
                public class Generic<TKey> : RapidRepo.Entities.BaseEntity<TKey> where TKey : notnull { }

                [GenerateUnitOfWork({{typeofExpression}})]
                public partial class ShopUnitOfWork : RapidRepo.UnitOfWork.UnitOfWorkBase<System.Guid>
                {
                    public ShopUnitOfWork() : base(System.Guid.Empty) { }
                }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source);

        sources.Should().BeEmpty();
        diagnostics.Should().ContainSingle(d => d.Id == "RRUOW004")
            .Which.GetMessage().Should().Contain($"'{typeName}'").And.Contain("neither");
    }

    [Fact]
    public void AttributeWithoutSources_GeneratesOnlyCustomRepositoryProperties()
    {
        var source = Entities + """
            namespace Shop
            {
                using RapidRepo.Attributes;
                using Shop.Catalog;

                public interface IProductRepository : RapidRepo.Repositories.Interfaces.IRepository<Product, int> { }

                [GenerateUnitOfWork]
                public partial class ShopUnitOfWork : RapidRepo.UnitOfWork.UnitOfWorkBase<System.Guid>
                {
                    public ShopUnitOfWork() : base(System.Guid.Empty) { }
                }
            }
            """;

        var (diagnostics, sources) = RunGenerator(source, includeEfCore: false);

        diagnostics.Should().BeEmpty();
        var cls = FindSource(sources, "ShopUnitOfWork.Repositories");
        cls.Should().Contain("public IProductRepository Products => ResolveRepository<IProductRepository>();");
        cls.Should().NotContain("GetRepository<");
    }

    [Fact]
    public void CustomRepositoryInterface_ForAnotherEntityUnderTheSameName_EmitsRRUOW002()
    {
        var source = """
            namespace Shop.Sales { public class Order : RapidRepo.Entities.BaseEntity<int> { } }
            namespace Shop.Archive
            {
                public class Order : RapidRepo.Entities.BaseEntity<int> { }
                public interface IOrderRepository : RapidRepo.Repositories.Interfaces.IRepository<Order, int> { }
            }

            namespace Shop
            {
                using RapidRepo.Attributes;

                [GenerateUnitOfWork(typeof(Shop.Sales.Order))]
                public partial class ShopUnitOfWork : RapidRepo.UnitOfWork.UnitOfWorkBase<System.Guid>
                {
                    public ShopUnitOfWork() : base(System.Guid.Empty) { }
                }
            }
            """;

        var (diagnostics, _) = RunGenerator(source, includeEfCore: false);

        diagnostics.Should().ContainSingle(d => d.Id == "RRUOW002")
            .Which.GetMessage().Should().Contain("'Orders'").And.Contain("IOrderRepository");
    }

    [Fact]
    public void ClosedGenericEntitySource_GeneratesCodeThatCompiles()
    {
        var source = """
            namespace Shop.Catalog
            {
                public class Tag<TValue> : RapidRepo.Entities.BaseEntity<int> { public TValue? Value { get; set; } }
            }

            namespace Shop
            {
                using RapidRepo.Attributes;
                using RapidRepo.Repositories.Interfaces;
                using Shop.Catalog;
                using System;
                using System.Threading;
                using System.Threading.Tasks;

                [GenerateUnitOfWork(typeof(Tag<string>))]
                public partial class ShopUnitOfWork : RapidRepo.UnitOfWork.UnitOfWorkBase<Guid>
                {
                    public ShopUnitOfWork() : base(Guid.Empty) { }

                    protected override IRepository<TEntity, TKey> CreateRepository<TEntity, TKey>() =>
                        throw new NotSupportedException();

                    protected override void CommitCore(Guid? userId) { }

                    protected override Task CommitCoreAsync(Guid? userId, CancellationToken cancellationToken) =>
                        Task.CompletedTask;
                }
            }
            """;

        var compilation = CreateCompilation(source, includeEfCore: false);
        CSharpGeneratorDriver.Create(new UnitOfWorkGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        generatorDiagnostics.Should().BeEmpty();
        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        output.SyntaxTrees.Select(t => t.ToString())
            .Should().Contain(t => t.Contains("IRepository<Tag<string>, int> Tags"));
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, IReadOnlyList<(string HintName, string Source)> Sources)
        RunGenerator(string source, bool includeEfCore = true)
    {
        var compilation = CreateCompilation(source, includeEfCore);

        var result = CSharpGeneratorDriver.Create(new UnitOfWorkGenerator())
            .RunGenerators(compilation)
            .GetRunResult()
            .Results[0];

        return (result.Diagnostics, result.GeneratedSources.Select(s => (s.HintName, s.SourceText.ToString())).ToList());
    }

    private static CSharpCompilation CreateCompilation(string source, bool includeEfCore)
    {
        // The full runtime reference set and no global usings: the generated code must compile on its own, also in
        // projects without <ImplicitUsings>enable</ImplicitUsings>.
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path).StartsWith("System.", StringComparison.Ordinal)
                || Path.GetFileName(path) is "mscorlib.dll" or "netstandard.dll")
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(RapidRepo.Entities.BaseEntity<>).Assembly.Location));

        if (includeEfCore)
            references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly.Location));

        return CSharpCompilation.Create(
            "EntitySourceAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));
    }

    private static string FindSource(IReadOnlyList<(string HintName, string Source)> sources, string hintNameContains)
        => sources.First(s => s.HintName.Contains(hintNameContains)).Source;
}

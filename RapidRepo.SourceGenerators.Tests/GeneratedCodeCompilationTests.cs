using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RapidRepo.SourceGenerators.Tests;

/// <summary>
/// The generated code has to compile in the consumer's project as it is, including projects without
/// <c>&lt;ImplicitUsings&gt;enable&lt;/ImplicitUsings&gt;</c>, so it must bring its own using directives.
/// </summary>
public class GeneratedCodeCompilationTests
{
    [Theory]
    [InlineData("System.Guid", "Guid", true)]
    [InlineData("System.DateTime", "DateTime", true)]
    [InlineData("int", "int", false)]
    [InlineData("long", "long", false)]
    [InlineData("string", "string", false)]
    [InlineData("Shop.Keys.Sku", "Sku", false)]
    public void DbSetKeyType_GeneratedCodeCompiles_WithoutGlobalUsings(string keyType, string keyDisplay, bool needsUsingSystem)
    {
        var source = $$"""
            namespace Shop.Keys
            {
                public readonly record struct Sku(string Value);
            }

            namespace Shop
            {
                using Microsoft.EntityFrameworkCore;
                using RapidRepo.Attributes;
                using RapidRepo.Entities;

                public class Product : BaseEntity<{{keyType}}> { }

                public class ShopDbContext : DbContext
                {
                    public DbSet<Product> Products { get; set; } = null!;
                }

                [GenerateUnitOfWork(typeof(ShopDbContext))]
                public partial class ShopUnitOfWork : RapidRepo.EntityFrameworkCore.EfUnitOfWork<System.Guid>
                {
                    public ShopUnitOfWork(ShopDbContext db) : base(db, System.Guid.Empty) { }
                }
            }
            """;

        var (output, generated) = Generate(source);

        output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var cls = generated.Single(s => s.HintName.Contains("ShopUnitOfWork.Repositories")).Source;
        cls.Should().Contain($"IRepository<Product, {keyDisplay}> Products => GetRepository<Product, {keyDisplay}>();");

        if (needsUsingSystem)
            cls.Should().Contain("using System;");
        else
            cls.Should().NotContain("using System;", "keyword key types need no using directive");
    }

    private static (Compilation Output, IReadOnlyList<(string HintName, string Source)> Generated) Generate(string source)
    {
        // The full runtime reference set and no global usings, like a project with implicit usings turned off.
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path).StartsWith("System.", StringComparison.Ordinal)
                || Path.GetFileName(path) is "mscorlib.dll" or "netstandard.dll")
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(RapidRepo.Entities.BaseEntity<>).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(RapidRepo.EntityFrameworkCore.EfUnitOfWork<>).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "CompilationTestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create(new UnitOfWorkGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var generated = driver.GetRunResult().Results[0].GeneratedSources
            .Select(s => (s.HintName, s.SourceText.ToString()))
            .ToList();

        return (output, generated);
    }
}

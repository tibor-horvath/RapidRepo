using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RapidRepo.SourceGenerators.Tests;

/// <summary>
/// The generator must not regenerate its files for edits that do not change them, or the IDE re-binds the code
/// that depends on them on every keystroke; and it must still pick up edits to other files that do change them.
/// </summary>
public class IncrementalCachingTests
{
    private const string UnitOfWorkSource = """
        using RapidRepo.Attributes;
        using RapidRepo.EntityFrameworkCore;
        using RapidRepo.Entities;
        using Microsoft.EntityFrameworkCore;

        namespace MyApp.Data;

        public class User : BaseEntity<int> { }

        public partial class AppDbContext : DbContext
        {
            public DbSet<User> Users { get; set; } = null!;
        }

        [GenerateUnitOfWork(typeof(AppDbContext))]
        public partial class AppUnitOfWork : EfUnitOfWork<int>
        {
            public AppUnitOfWork(AppDbContext ctx) : base(ctx, 0) { }
        }
        """;

    [Fact]
    public void UnrelatedEdit_LeavesResultUnchanged_AndOutputCached()
    {
        var other = CSharpSyntaxTree.ParseText("namespace MyApp; public class Unrelated { }");
        var compilation = CreateCompilation(CSharpSyntaxTree.ParseText(UnitOfWorkSource), other);

        GeneratorDriver driver = CreateDriver();
        driver = driver.RunGenerators(compilation);

        var edited = compilation.ReplaceSyntaxTree(
            other,
            CSharpSyntaxTree.ParseText("namespace MyApp; public class Unrelated { public int Value { get; set; } }"));
        driver = driver.RunGenerators(edited);

        var run = driver.GetRunResult().Results[0];
        run.TrackedSteps[UnitOfWorkGenerator.ResultStepName]
            .SelectMany(step => step.Outputs)
            .Should().OnlyContain(output => output.Reason == IncrementalStepRunReason.Unchanged);
        run.TrackedOutputSteps.SelectMany(step => step.Value).SelectMany(step => step.Outputs)
            .Should().OnlyContain(output => output.Reason == IncrementalStepRunReason.Cached);
    }

    [Fact]
    public void EditToAnotherFile_ThatAddsADbSet_RegeneratesTheProperty()
    {
        var context = CSharpSyntaxTree.ParseText("""
            namespace MyApp.Data;
            public partial class AppDbContext { }
            """);
        var compilation = CreateCompilation(CSharpSyntaxTree.ParseText(UnitOfWorkSource), context);

        GeneratorDriver driver = CreateDriver();
        driver = driver.RunGenerators(compilation);

        var edited = compilation.ReplaceSyntaxTree(context, CSharpSyntaxTree.ParseText("""
            using Microsoft.EntityFrameworkCore;
            using RapidRepo.Entities;

            namespace MyApp.Data;

            public class Order : BaseEntity<long> { }

            public partial class AppDbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;
            }
            """));
        driver = driver.RunGenerators(edited);

        var generated = driver.GetRunResult().Results[0].GeneratedSources
            .Single(s => s.HintName.Contains("AppUnitOfWork.Repositories"))
            .SourceText.ToString();

        generated.Should().Contain("Orders => GetRepository<Order, long>()");
    }

    private static GeneratorDriver CreateDriver() =>
        CSharpGeneratorDriver.Create(
            [new UnitOfWorkGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

    private static CSharpCompilation CreateCompilation(params SyntaxTree[] trees) => GeneratorHarness.Compile(trees);
}

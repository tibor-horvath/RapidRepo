using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RapidRepo.SourceGenerators.Tests;

/// <summary>
/// Builds the compilations the generator runs against and runs it, so every test class shares one set of
/// references and one way of reading the output.
/// </summary>
internal static class GeneratorHarness
{
    /// <summary>The core runtime only: enough to bind the samples, with no other System assemblies.</summary>
    public static IReadOnlyList<MetadataReference> MinimalRuntime { get; } =
    [
        FromAssemblyOf<object>(),
        MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
    ];

    /// <summary>
    /// The full runtime reference set and no global usings, like a project with implicit usings turned off: the
    /// generated code must compile on its own.
    /// </summary>
    public static IReadOnlyList<MetadataReference> FullRuntime { get; } =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path).StartsWith("System.", StringComparison.Ordinal)
                || Path.GetFileName(path) is "mscorlib.dll" or "netstandard.dll")
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();

    /// <summary>RapidRepo core: attributes, entities and repository contracts.</summary>
    public static MetadataReference CoreReference { get; } = FromAssemblyOf<RapidRepo.Entities.BaseEntity<int>>();

    /// <summary>
    /// RapidRepo.EntityFrameworkCore. The samples keep the 8.x <c>UnitOfWork&lt;T&gt;</c> name, which now lives
    /// there as an obsolete shim, so the tests also prove that existing declarations still generate.
    /// </summary>
    public static MetadataReference EfProviderReference { get; } =
        FromAssemblyOf<RapidRepo.EntityFrameworkCore.EfUnitOfWork<int>>();

    public static MetadataReference EfCoreReference { get; } =
        FromAssemblyOf<Microsoft.EntityFrameworkCore.DbContext>();

    /// <summary>The usual set: the minimal runtime, RapidRepo, its EF Core provider and EF Core.</summary>
    public static IReadOnlyList<MetadataReference> Default { get; } =
        [.. MinimalRuntime, CoreReference, EfProviderReference, EfCoreReference];

    public static CSharpCompilation Compile(string source, IEnumerable<MetadataReference>? references = null) =>
        Compile([CSharpSyntaxTree.ParseText(source)], references);

    public static CSharpCompilation Compile(IEnumerable<SyntaxTree> syntaxTrees, IEnumerable<MetadataReference>? references = null) =>
        CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references ?? Default,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

    /// <summary>Runs the generator once over <paramref name="compilation"/>.</summary>
    public static (ImmutableArray<Diagnostic> Diagnostics, IReadOnlyList<(string HintName, string Source)> Sources)
        Run(Compilation compilation)
    {
        var result = CSharpGeneratorDriver.Create(new UnitOfWorkGenerator())
            .RunGenerators(compilation)
            .GetRunResult()
            .Results[0];

        return (result.Diagnostics, result.GeneratedSources.Select(s => (s.HintName, s.SourceText.ToString())).ToList());
    }

    /// <summary>Gets the generated file whose hint name contains <paramref name="hintNameContains"/>.</summary>
    public static string FindSource(IReadOnlyList<(string HintName, string Source)> sources, string hintNameContains) =>
        sources.First(s => s.HintName.Contains(hintNameContains)).Source;

    private static MetadataReference FromAssemblyOf<T>() => MetadataReference.CreateFromFile(typeof(T).Assembly.Location);
}

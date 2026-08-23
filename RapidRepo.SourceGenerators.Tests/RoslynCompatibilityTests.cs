using System.Reflection;
using FluentAssertions;
using RapidRepo.SourceGenerators;

namespace RapidRepo.SourceGenerators.Tests;

/// <summary>
/// Guards the Roslyn version RapidRepo.SourceGenerators is compiled against.
///
/// That version is the minimum compiler a consumer needs in order to load the generator at
/// all: Roslyn refuses to load an analyzer referencing a newer Microsoft.CodeAnalysis than
/// the running compiler, reports CS9057 as a *warning*, and silently skips generation. The
/// consumer then fails with CS0246 errors about types that were never emitted.
///
/// Every other test here drives the generator as a plain library, so none of them can catch
/// a bump. These can.
/// </summary>
public class RoslynCompatibilityTests
{
    /// <summary>
    /// Highest Roslyn the generator may reference. Raise this only as a deliberate breaking
    /// change: it raises the minimum SDK every consumer must be on.
    /// </summary>
    private static readonly Version MaxSupportedRoslynVersion = new(4, 13, 0, 0);

    private static readonly string[] RoslynAssemblies =
    [
        "Microsoft.CodeAnalysis",
        "Microsoft.CodeAnalysis.CSharp"
    ];

    [Fact]
    public void Generator_Does_Not_Reference_A_Newer_Roslyn_Than_Supported()
    {
        var referenced = typeof(UnitOfWorkGenerator).Assembly
            .GetReferencedAssemblies()
            .Where(a => RoslynAssemblies.Contains(a.Name))
            .ToList();

        referenced.Should().NotBeEmpty("the generator must reference Roslyn");

        foreach (var assembly in referenced)
        {
            assembly.Version.Should().BeLessThanOrEqualTo(
                MaxSupportedRoslynVersion,
                "{0} {1} would raise the minimum compiler consumers need; Roslyn skips the " +
                "generator with CS9057 on anything older and their builds fail with CS0246",
                assembly.Name,
                assembly.Version);
        }
    }

    [Fact]
    public void Generator_Targets_NetStandard20()
    {
        var targetFramework = typeof(UnitOfWorkGenerator).Assembly
            .GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();

        targetFramework!.FrameworkName.Should().Be(
            ".NETStandard,Version=v2.0",
            "analyzers must target netstandard2.0 to load in every compiler host");
    }
}

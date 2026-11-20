using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RapidRepo.SourceGenerators;

[Generator]
public sealed class UnitOfWorkGenerator : IIncrementalGenerator
{
    private const string AttributeFqn = "RapidRepo.Attributes.GenerateUnitOfWorkAttribute";
    private const string DbContextFqn = "Microsoft.EntityFrameworkCore.DbContext";
    private const string BaseEntityFqn = "RapidRepo.Entities.BaseEntity`1";

    /// <summary>The tracking name of the step that builds each class's <see cref="GenerationResult"/>.</summary>
    internal const string ResultStepName = "UnitOfWorkResult";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Only the class's metadata name leaves the attribute step. A symbol would pin the compilation it came from,
        // and be reused, stale, after edits to other files that the attribute step does not re-run for.
        var classNames = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFqn,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => ctx.TargetSymbol is INamedTypeSymbol type ? GetMetadataName(type) : null)
            .Where(static name => name is not null)
            .Select(static (name, _) => name!);

        // The discovery reads the whole compilation (DbSets, custom repository interfaces in any file), so it runs on
        // every compilation change. Its result is value-equal, so the output step below runs only when it differs.
        var results = classNames
            .Combine(context.CompilationProvider)
            .Select(static (pair, cancellationToken) => BuildResult(pair.Left, pair.Right, cancellationToken))
            .WithTrackingName(ResultStepName);

        context.RegisterSourceOutput(results, static (ctx, result) => AddOutput(ctx, result));
    }

    private static void AddOutput(SourceProductionContext context, GenerationResult result)
    {
        foreach (var diagnostic in result.Diagnostics)
            context.ReportDiagnostic(diagnostic.ToDiagnostic());

        foreach (var file in result.Files)
            context.AddSource(file.HintName, file.Source);
    }

    private static GenerationResult BuildResult(string metadataName, Compilation compilation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var classSymbol = compilation.Assembly.GetTypeByMetadataName(metadataName);
        if (classSymbol is null)
            return GenerationResult.Empty;

        var diagnostics = new List<DiagnosticInfo>();
        var files = BuildFiles(diagnostics, classSymbol, compilation);

        return new GenerationResult(
            new EquatableArray<GeneratedFile>(files.ToImmutableArray()),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutableArray()));
    }

    private static string GetMetadataName(INamedTypeSymbol type)
    {
        var name = type.MetadataName;
        for (var containing = type.ContainingType; containing is not null; containing = containing.ContainingType)
            name = $"{containing.MetadataName}+{name}";

        return type.ContainingNamespace is { IsGlobalNamespace: false } ns ? $"{ns.ToDisplayString()}.{name}" : name;
    }

    /// <summary>
    /// Builds the generated files for <paramref name="classSymbol"/>, adding any problem found to
    /// <paramref name="diagnostics"/>. Returns no files when a problem stops generation.
    /// </summary>
    private static List<GeneratedFile> BuildFiles(
        List<DiagnosticInfo> diagnostics,
        INamedTypeSymbol classSymbol,
        Compilation compilation)
    {
        var files = new List<GeneratedFile>();

        // RRUOW003: class must be partial
        var isPartial = classSymbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<ClassDeclarationSyntax>()
            .Any(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

        if (!isPartial)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                Diagnostics.NotPartial,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return files;
        }

        // Get attribute
        var attr = classSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AttributeFqn);
        if (attr is null) return files;

        // Parse sources — RRUOW004 if any type is neither a DbContext nor an entity
        var sources = ParseSources(attr, compilation, diagnostics, classSymbol);
        if (sources is null) return files;

        // Parse optional settings
        var interfaceName = attr.NamedArguments
            .FirstOrDefault(n => n.Key == "RepositoriesInterfaceName").Value.Value as string
            ?? $"I{classSymbol.Name}Repositories";

        var includeNamespaces = ParseStringArray(attr, "IncludeNamespaces");
        var excludeNamespaces = ParseStringArray(attr, "ExcludeNamespaces");

        // Discover properties
        var properties = PropertyDiscoverer.Discover(
            compilation, diagnostics, classSymbol, sources, includeNamespaces, excludeNamespaces);

        if (properties.Count == 0) return files;

        // Emit
        var ns = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : classSymbol.ContainingNamespace.ToDisplayString();

        files.Add(new GeneratedFile(
            BuildHintName(ns, interfaceName),
            SourceEmitter.EmitInterface(ns, interfaceName, properties)));

        files.Add(new GeneratedFile(
            BuildHintName(ns, $"{classSymbol.Name}.Repositories"),
            SourceEmitter.EmitPartialClass(ns, classSymbol.Name, properties)));

        return files;
    }

    private static string BuildHintName(string? namespaceName, string baseName)
    {
        var stem = namespaceName is null ? baseName : $"{namespaceName}.{baseName}";
        return $"{SanitizeMetadataName(stem)}.g.cs";
    }

    private static string SanitizeMetadataName(string name)
    {
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] is ':' or '\\' or '/' or '*' or '?' or '"' or '<' or '>' or '|')
                chars[i] = '_';
        }

        return new string(chars);
    }

    /// <summary>
    /// Classifies each type passed to the attribute as a DbContext or an entity. DbContext is matched by metadata
    /// name, so the generator needs no EF Core reference and works for consumers that do not use EF Core.
    /// </summary>
    private static List<UnitOfWorkSource>? ParseSources(
        AttributeData attr,
        Compilation compilation,
        List<DiagnosticInfo> diagnostics,
        INamedTypeSymbol classSymbol)
    {
        var dbContextBase = compilation.GetTypeByMetadataName(DbContextFqn);
        var baseEntityDef = compilation.GetTypeByMetadataName(BaseEntityFqn);

        if (attr.ConstructorArguments.Length == 0 || attr.ConstructorArguments[0].Values.IsEmpty)
            return new List<UnitOfWorkSource>();

        var result = new List<UnitOfWorkSource>();

        foreach (var arg in attr.ConstructorArguments[0].Values)
        {
            if (arg.Value is not INamedTypeSymbol typeSymbol) continue;

            if (dbContextBase is not null && InheritsFrom(typeSymbol, dbContextBase))
            {
                result.Add(UnitOfWorkSource.DbContext(typeSymbol));
                continue;
            }

            if (baseEntityDef is not null
                && !typeSymbol.IsUnboundGenericType
                && PropertyDiscoverer.FindBaseEntityKeyType(typeSymbol, baseEntityDef) is { } keyType)
            {
                result.Add(UnitOfWorkSource.Entity(typeSymbol, keyType));
                continue;
            }

            diagnostics.Add(DiagnosticInfo.Create(
                Diagnostics.InvalidSource,
                classSymbol.Locations.FirstOrDefault(),
                typeSymbol.Name));
            return null;
        }

        return result;
    }

    private static ImmutableArray<string> ParseStringArray(AttributeData attr, string argName)
    {
        var namedArg = default(KeyValuePair<string, TypedConstant>);
        foreach (var n in attr.NamedArguments)
        {
            if (n.Key == argName) { namedArg = n; break; }
        }

        if (namedArg.Key is null) return ImmutableArray<string>.Empty;

        var builder = ImmutableArray.CreateBuilder<string>();
        foreach (var v in namedArg.Value.Values)
        {
            if (v.Value is string s) builder.Add(s);
        }
        return builder.ToImmutable();
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType.OriginalDefinition))
                return true;
            current = current.BaseType;
        }
        return false;
    }
}

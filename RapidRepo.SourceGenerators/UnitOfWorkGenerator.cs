using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classProvider = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFqn,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => ctx.TargetSymbol as INamedTypeSymbol)
            .Where(static s => s is not null)
            .Select(static (s, _) => s!);

        context.RegisterSourceOutput(
            classProvider.Combine(context.CompilationProvider),
            static (ctx, pair) => Execute(ctx, pair.Left, pair.Right));
    }

    private static void Execute(
        SourceProductionContext ctx,
        INamedTypeSymbol classSymbol,
        Compilation compilation)
    {
        // RRUOW003: class must be partial
        var isPartial = classSymbol.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<ClassDeclarationSyntax>()
            .Any(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

        if (!isPartial)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                Diagnostics.NotPartial,
                classSymbol.Locations.FirstOrDefault(),
                classSymbol.Name));
            return;
        }

        // Get attribute
        var attr = classSymbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == AttributeFqn);
        if (attr is null) return;

        // Parse sources — RRUOW004 if any type is neither a DbContext nor an entity
        var sources = ParseSources(attr, compilation, ctx, classSymbol);
        if (sources is null) return;

        // Parse optional settings
        var interfaceName = attr.NamedArguments
            .FirstOrDefault(n => n.Key == "RepositoriesInterfaceName").Value.Value as string
            ?? $"I{classSymbol.Name}Repositories";

        var includeNamespaces = ParseStringArray(attr, "IncludeNamespaces");
        var excludeNamespaces = ParseStringArray(attr, "ExcludeNamespaces");

        // Discover properties
        var properties = PropertyDiscoverer.Discover(
            compilation, ctx, classSymbol, sources, includeNamespaces, excludeNamespaces);

        if (properties.Count == 0) return;

        // Emit
        var ns = classSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : classSymbol.ContainingNamespace.ToDisplayString();

        ctx.AddSource(
            BuildHintName(ns, interfaceName),
            SourceEmitter.EmitInterface(ns, interfaceName, properties));

        ctx.AddSource(
            BuildHintName(ns, $"{classSymbol.Name}.Repositories"),
            SourceEmitter.EmitPartialClass(ns, classSymbol.Name, properties));
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
        SourceProductionContext ctx,
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

            ctx.ReportDiagnostic(Diagnostic.Create(
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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RapidRepo.SourceGenerators;

internal static class PropertyDiscoverer
{
    private const string IRepositoryFqn       = "RapidRepo.Repositories.Interfaces.IRepository`2";
    private const string IReadOnlyRepoFqn     = "RapidRepo.Repositories.Interfaces.IReadOnlyRepository`2";
    private const string IWriteRepoFqn        = "RapidRepo.Repositories.Interfaces.IWriteRepository`2";
    private const string BaseEntityFqn        = "RapidRepo.Entities.BaseEntity`1";
    private const string DbSetFqn             = "Microsoft.EntityFrameworkCore.DbSet`1";
    private const string DbContextFqn         = "Microsoft.EntityFrameworkCore.DbContext";
    private const string UoWPropertyAttrFqn   = "RapidRepo.Attributes.UnitOfWorkPropertyAttribute";

    private static readonly SymbolDisplayFormat RepositoryTypeArgumentFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    internal static IReadOnlyList<PropertyModel> Discover(
        Compilation compilation,
        List<DiagnosticInfo> diagnostics,
        INamedTypeSymbol classSymbol,
        IReadOnlyList<UnitOfWorkSource> sources,
        ImmutableArray<string> includeNamespaces,
        ImmutableArray<string> excludeNamespaces)
    {
        var iRepositoryDef    = compilation.GetTypeByMetadataName(IRepositoryFqn);
        var iReadOnlyRepoDef  = compilation.GetTypeByMetadataName(IReadOnlyRepoFqn);
        var iWriteRepoDef     = compilation.GetTypeByMetadataName(IWriteRepoFqn);
        var baseEntityDef     = compilation.GetTypeByMetadataName(BaseEntityFqn);
        var dbSetDef          = compilation.GetTypeByMetadataName(DbSetFqn);

        // entity type symbol → PropertyModel (DbSet-backed, may be replaced by custom repo)
        var byEntity = new Dictionary<ITypeSymbol, PropertyModel>(SymbolEqualityComparer.Default);
        // property name → entity (for RRUOW002 detection)
        var nameToEntity = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);

        // ── 1. Generic repository properties: DbSet-backed and entity sources ─
        if (baseEntityDef is not null && iRepositoryDef is not null)
        {
            foreach (var source in sources)
            {
                if (source.IsEntity)
                {
                    AddGenericRepository(Pluralizer.PluralizeNoun(source.Type.Name), source.Type, source.EntityKeyType!);
                    continue;
                }

                if (dbSetDef is null) continue;

                foreach (var member in GetAllMembers(source.Type))
                {
                    if (member is not IPropertySymbol prop) continue;
                    if (prop.IsStatic || prop.DeclaredAccessibility != Accessibility.Public) continue;
                    if (prop.Type is not INamedTypeSymbol propType) continue;
                    if (!SymbolEqualityComparer.Default.Equals(propType.OriginalDefinition, dbSetDef)) continue;

                    var entityType = propType.TypeArguments[0] as INamedTypeSymbol;
                    if (entityType is null) continue;

                    // RRUOW001: entity must inherit BaseEntity<TKey>
                    var keyType = FindBaseEntityKeyType(entityType, baseEntityDef);
                    if (keyType is null)
                    {
                        diagnostics.Add(DiagnosticInfo.Create(
                            Diagnostics.EntityNotBaseEntity,
                            classSymbol.Locations.FirstOrDefault(),
                            entityType.Name));
                        continue;
                    }

                    AddGenericRepository(prop.Name, entityType, keyType);
                }
            }
        }

        // Adds an IRepository<TEntity, TKey> property backed by GetRepository<TEntity, TKey>(). The first source to
        // supply an entity wins; a different entity under an existing property name is RRUOW002.
        void AddGenericRepository(string propertyName, INamedTypeSymbol entityType, ITypeSymbol keyType)
        {
            var entityTypeDisplay = entityType.ToDisplayString(RepositoryTypeArgumentFormat);
            var keyTypeDisplay    = keyType.ToDisplayString(RepositoryTypeArgumentFormat);

            var entityNs   = entityType.ContainingNamespace.ToDisplayString();
            var keyNs      = keyType.ContainingNamespace?.ToDisplayString();
            var namespaces = new List<string> { "RapidRepo.Repositories.Interfaces", entityNs };

            // A key printed as a C# keyword (int, long, string) needs no using; anything printed by name does, also
            // System types such as Guid, or the generated code only compiles with implicit usings.
            if (keyNs is not null && SyntaxFacts.GetKeywordKind(keyTypeDisplay) == SyntaxKind.None)
                namespaces.Add(keyNs);
            var typeDisplay       = $"IRepository<{entityTypeDisplay}, {keyTypeDisplay}>";
            var impl              = $"GetRepository<{entityTypeDisplay}, {keyTypeDisplay}>()";

            var model = new PropertyModel(propertyName, typeDisplay, impl, namespaces);

            if (byEntity.ContainsKey(entityType))
                return;

            if (nameToEntity.TryGetValue(propertyName, out var existingEntity)
                && !SymbolEqualityComparer.Default.Equals(existingEntity, entityType))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    Diagnostics.DuplicatePropertyName,
                    classSymbol.Locations.FirstOrDefault(),
                    propertyName,
                    byEntity[existingEntity].TypeDisplay,
                    typeDisplay));
                return;
            }

            byEntity[entityType]        = model;
            nameToEntity[propertyName]  = entityType;
        }

        // ── 2. Custom repository interfaces (consumer assembly only) ──────────
        if (iRepositoryDef is not null || iReadOnlyRepoDef is not null || iWriteRepoDef is not null)
        {
            var uowPropAttrDef = compilation.GetTypeByMetadataName(UoWPropertyAttrFqn);

            foreach (var type in compilation.Assembly.GlobalNamespace.GetAllTypes())
            {
                if (!type.IsType || type.TypeKind != TypeKind.Interface) continue;
                if (type.IsGenericType) continue;

                var ns = type.ContainingNamespace.ToDisplayString();
                if (!PassesNamespaceFilters(ns, includeNamespaces, excludeNamespaces)) continue;

                // Find the entity and key type via implemented repo interfaces
                ITypeSymbol? entityType = null;
                ITypeSymbol? keyType    = null;

                foreach (var iface in type.AllInterfaces)
                {
                    if (!iface.IsGenericType) continue;
                    var def = iface.OriginalDefinition;
                    if (SymbolEqualityComparer.Default.Equals(def, iRepositoryDef)
                        || SymbolEqualityComparer.Default.Equals(def, iReadOnlyRepoDef)
                        || SymbolEqualityComparer.Default.Equals(def, iWriteRepoDef))
                    {
                        entityType = iface.TypeArguments[0];
                        keyType    = iface.TypeArguments[1];
                        break;
                    }
                }

                if (entityType is null || keyType is null) continue;

                // Derive property name
                string propertyName;
                var uowPropAttr = uowPropAttrDef is not null
                    ? type.GetAttributes().FirstOrDefault(a =>
                        SymbolEqualityComparer.Default.Equals(a.AttributeClass?.OriginalDefinition, uowPropAttrDef))
                    : null;

                if (uowPropAttr?.ConstructorArguments.Length > 0
                    && uowPropAttr.ConstructorArguments[0].Value is string overrideName
                    && !string.IsNullOrWhiteSpace(overrideName))
                {
                    propertyName = overrideName;
                }
                else
                {
                    propertyName = Pluralizer.Pluralize(type.Name);
                }

                // RRUOW002: duplicate property name from a different entity
                if (nameToEntity.TryGetValue(propertyName, out var existingEntity)
                    && !SymbolEqualityComparer.Default.Equals(existingEntity, entityType)
                    && !byEntity.ContainsKey(entityType))
                {
                    diagnostics.Add(DiagnosticInfo.Create(
                        Diagnostics.DuplicatePropertyName,
                        classSymbol.Locations.FirstOrDefault(),
                        propertyName,
                        byEntity[existingEntity].TypeDisplay,
                        type.Name));
                    continue;
                }

                var ifaceNs    = type.ContainingNamespace.ToDisplayString();
                var namespaces = new List<string> { ifaceNs };

                var model = new PropertyModel(propertyName, type.Name, $"ResolveRepository<{type.Name}>()", namespaces);

                // Custom repo replaces DbSet-backed entry for the same entity
                if (byEntity.TryGetValue(entityType, out var existing))
                    nameToEntity.Remove(existing.Name);

                byEntity[entityType]         = model;
                nameToEntity[propertyName]   = entityType;
            }
        }

        return byEntity.Values.OrderBy(p => p.Name).ToList();
    }

    internal static ITypeSymbol? FindBaseEntityKeyType(INamedTypeSymbol entity, INamedTypeSymbol baseEntityDef)
    {
        var current = entity.BaseType;
        while (current is not null)
        {
            if (current.IsGenericType
                && SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseEntityDef))
            {
                return current.TypeArguments[0];
            }
            current = current.BaseType;
        }
        return null;
    }

    private static IEnumerable<ISymbol> GetAllMembers(INamedTypeSymbol type)
    {
        var current = (INamedTypeSymbol?)type;
        while (current is not null)
        {
            foreach (var m in current.GetMembers())
                yield return m;
            current = current.BaseType;
        }
    }

    private static bool PassesNamespaceFilters(
        string ns,
        ImmutableArray<string> includes,
        ImmutableArray<string> excludes)
    {
        foreach (var ex in excludes)
            if (MatchesNamespacePrefix(ns, ex)) return false;

        if (includes.IsEmpty) return true;

        foreach (var inc in includes)
            if (MatchesNamespacePrefix(ns, inc)) return true;

        return false;
    }

    private static bool MatchesNamespacePrefix(string ns, string prefix) =>
        string.Equals(ns, prefix, StringComparison.Ordinal)
        || ns.StartsWith(prefix + ".", StringComparison.Ordinal);
}

internal static class NamespaceExtensions
{
    internal static IEnumerable<INamedTypeSymbol> GetAllTypes(this INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
            yield return type;

        foreach (var childNs in ns.GetNamespaceMembers())
            foreach (var type in childNs.GetAllTypes())
                yield return type;
    }
}

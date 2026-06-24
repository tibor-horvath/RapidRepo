using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

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

    internal static IReadOnlyList<PropertyModel> Discover(
        Compilation compilation,
        SourceProductionContext ctx,
        INamedTypeSymbol classSymbol,
        IReadOnlyList<INamedTypeSymbol> dbContextTypes,
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

        // ── 1. DbSet-backed properties ────────────────────────────────────────
        if (dbSetDef is not null && baseEntityDef is not null && iRepositoryDef is not null)
        {
            foreach (var dbCtx in dbContextTypes)
            {
                foreach (var member in GetAllMembers(dbCtx))
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
                        ctx.ReportDiagnostic(Diagnostic.Create(
                            Diagnostics.EntityNotBaseEntity,
                            classSymbol.Locations.FirstOrDefault(),
                            entityType.Name));
                        continue;
                    }

                    var entityNs   = entityType.ContainingNamespace.ToDisplayString();
                    var keyNs      = keyType.ContainingNamespace?.ToDisplayString();
                    var namespaces = new List<string> { "RapidRepo.Repositories.Interfaces", entityNs };
                    if (keyNs is not null && !keyNs.StartsWith("System", StringComparison.Ordinal))
                        namespaces.Add(keyNs);

                    var keyName     = GetCSharpTypeName(keyType);
                    var typeDisplay = $"IRepository<{entityType.Name}, {keyName}>";
                    var impl        = $"GetRepository<{entityType.Name}, {keyName}>()";

                    var model = new PropertyModel(prop.Name, typeDisplay, impl, namespaces);

                    if (!byEntity.ContainsKey(entityType))
                    {
                        byEntity[entityType]     = model;
                        nameToEntity[prop.Name]  = entityType;
                    }
                }
            }
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
                    ctx.ReportDiagnostic(Diagnostic.Create(
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

    private static string GetCSharpTypeName(ITypeSymbol type) => type.SpecialType switch
    {
        SpecialType.System_Boolean => "bool",
        SpecialType.System_Byte    => "byte",
        SpecialType.System_Char    => "char",
        SpecialType.System_Decimal => "decimal",
        SpecialType.System_Double  => "double",
        SpecialType.System_Single  => "float",
        SpecialType.System_Int16   => "short",
        SpecialType.System_Int32   => "int",
        SpecialType.System_Int64   => "long",
        SpecialType.System_String  => "string",
        _                          => type.Name,
    };

    private static ITypeSymbol? FindBaseEntityKeyType(INamedTypeSymbol entity, INamedTypeSymbol baseEntityDef)
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
            if (ns.StartsWith(ex, StringComparison.Ordinal)) return false;

        if (includes.IsEmpty) return true;

        foreach (var inc in includes)
            if (ns.StartsWith(inc, StringComparison.Ordinal)) return true;

        return false;
    }
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

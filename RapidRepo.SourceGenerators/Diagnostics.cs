using Microsoft.CodeAnalysis;

namespace RapidRepo.SourceGenerators;

internal static class Diagnostics
{
    private const string Category = "RapidRepo";

    internal static readonly DiagnosticDescriptor EntityNotBaseEntity = new(
        id: "RRUOW001",
        title: "Entity does not inherit BaseEntity<TKey>",
        messageFormat: "DbSet entity '{0}' does not inherit 'RapidRepo.Entities.BaseEntity<TKey>'. The property will be skipped.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor DuplicatePropertyName = new(
        id: "RRUOW002",
        title: "Duplicate repository property name",
        messageFormat: "Two repository interfaces resolve to the same property name '{0}': '{1}' and '{2}'.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor NotPartial = new(
        id: "RRUOW003",
        title: "Unit of Work class must be partial",
        messageFormat: "'{0}' is decorated with [GenerateUnitOfWork] but is not declared as 'partial'. Add the 'partial' modifier.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor NotDbContext = new(
        id: "RRUOW004",
        title: "Type passed to [GenerateUnitOfWork] is not a DbContext",
        messageFormat: "'{0}' does not inherit 'Microsoft.EntityFrameworkCore.DbContext'. Only DbContext-derived types can be passed to [GenerateUnitOfWork].",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}

using Microsoft.CodeAnalysis;

namespace RapidRepo.SourceGenerators;

/// <summary>
/// A type passed to <c>[GenerateUnitOfWork]</c>: either a DbContext, whose <c>DbSet&lt;TEntity&gt;</c> properties
/// supply the entities, or a single entity type.
/// </summary>
internal sealed class UnitOfWorkSource
{
    private UnitOfWorkSource(INamedTypeSymbol type, ITypeSymbol? entityKeyType)
    {
        Type = type;
        EntityKeyType = entityKeyType;
    }

    internal INamedTypeSymbol Type { get; }

    /// <summary>The key type when the source is an entity; <see langword="null"/> for a DbContext.</summary>
    internal ITypeSymbol? EntityKeyType { get; }

    internal bool IsEntity => EntityKeyType is not null;

    internal static UnitOfWorkSource DbContext(INamedTypeSymbol type) => new(type, entityKeyType: null);

    internal static UnitOfWorkSource Entity(INamedTypeSymbol type, ITypeSymbol keyType) => new(type, keyType);
}

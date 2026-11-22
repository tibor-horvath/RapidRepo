using Microsoft.EntityFrameworkCore.Metadata;
using RapidRepo.Tenancy;
using System.Runtime.CompilerServices;

namespace RapidRepo.EntityFrameworkCore.Internal;

/// <summary>
/// Which entity types a query rooted at an entity type can read: the type itself, its derived types, and every type
/// reached from them through navigations. A query that lifts filters lifts them on all of these, so the tenant filter
/// is at stake whenever one of them is a tenant entity, even if the root is not.
/// </summary>
internal static class TenantReachability
{
    private static readonly ConditionalWeakTable<IEntityType, IReadOnlyList<IEntityType>> Reachable = new();

    private static readonly ConditionalWeakTable<IModel, StrongBox<bool>> ModelsWithTenantData = new();

    /// <summary>
    /// Gets the entity types a query rooted at <paramref name="entityType"/> can read, <paramref name="entityType"/>
    /// included.
    /// </summary>
    public static IReadOnlyList<IEntityType> GetReachableTypes(IEntityType entityType) =>
        Reachable.GetValue(entityType, Walk);

    /// <summary>
    /// Gets a value indicating whether <paramref name="model"/> maps a tenant entity. Lifting every filter of a query
    /// in such a model is never safe: the caller can join any tenant entity set into it, and EF Core lifts filters for
    /// the whole query, joins included.
    /// </summary>
    public static bool HasTenantData(IModel model) =>
        ModelsWithTenantData.GetValue(
            model,
            m => new StrongBox<bool>(m.GetEntityTypes().Any(t => TenantTraits.IsTenantEntity(t.ClrType)))).Value;

    private static IReadOnlyList<IEntityType> Walk(IEntityType start)
    {
        var seen = new HashSet<IEntityType>();
        var pending = new Stack<IEntityType>();
        pending.Push(start);

        while (pending.TryPop(out var current))
        {
            foreach (var type in current.GetDerivedTypesInclusive())
            {
                if (!seen.Add(type))
                    continue;

                foreach (var navigation in type.GetNavigations())
                    pending.Push(navigation.TargetEntityType);

                foreach (var navigation in type.GetSkipNavigations())
                {
                    pending.Push(navigation.TargetEntityType);
                    pending.Push(navigation.JoinEntityType);
                }
            }
        }

        return [.. seen];
    }
}

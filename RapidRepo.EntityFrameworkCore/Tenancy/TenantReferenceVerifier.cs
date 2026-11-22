using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using RapidRepo.Tenancy;
using System.Runtime.CompilerServices;

namespace RapidRepo.EntityFrameworkCore.Internal;

/// <summary>
/// Refuses a commit whose added or updated entities refer to a row of another tenant through a foreign key: one
/// <c>SELECT</c> per referenced key, for the rows that match and belong to another tenant.
/// </summary>
/// <remarks>
/// <para>
/// A foreign key of a tenant entity that maps its <c>TenantId</c> to the principal's <c>TenantId</c> is skipped: the
/// commit checks the dependent's <c>TenantId</c>, and the database keeps the reference within it. The same foreign key
/// on an entity that is not a tenant entity is checked: nothing checks that entity's <c>TenantId</c>, so a composite
/// key alone does not keep the reference within the current tenant. A reference to a row added in the same commit,
/// which the guard stamps with the current tenant, and a reference an update does not change, are skipped too.
/// </para>
/// <para>
/// The database compares the keys, not this class, so a case-insensitive collation matches the same rows here as in
/// the foreign key constraint.
/// </para>
/// </remarks>
internal static class TenantReferenceVerifier
{
    private static readonly ConditionalWeakTable<IEntityType, IForeignKey[]> ForeignKeys = new();

    /// <summary>
    /// Gets a value indicating whether <paramref name="entityType"/> has a foreign key this class checks.
    /// </summary>
    public static bool HasTenantReferences(IEntityType entityType) => GetForeignKeys(entityType).Length > 0;

    /// <summary>
    /// Checks the references of <paramref name="entries"/>, the added and modified entries with a foreign key to a
    /// tenant entity, and throws on the first one that points at another tenant's row. <paramref name="added"/> is
    /// every entry the commit adds, whatever its type.
    /// </summary>
    /// <exception cref="TenantViolationException">A reference points at another tenant's row.</exception>
    public static async Task VerifyAsync(
        DbContext context,
        TenantCommitGuard guard,
        IReadOnlyList<EntityEntry> entries,
        IReadOnlyList<EntityEntry> added,
        bool async,
        CancellationToken cancellationToken)
    {
        if (guard.IsHostScope || entries.Count == 0)
            return;

        var addedKeys = new Dictionary<IKey, HashSet<KeyValues>>();
        var references = new Dictionary<IKey, Dictionary<KeyValues, List<(EntityEntry Entry, IForeignKey ForeignKey)>>>();

        foreach (var entry in entries)
        {
            foreach (var foreignKey in GetForeignKeys(entry.Metadata))
            {
                if (entry.State == EntityState.Modified
                    && !foreignKey.Properties.Any(p => entry.Property(p.Name).IsModified))
                {
                    continue;
                }

                var values = foreignKey.Properties.Select(p => entry.Property(p.Name).CurrentValue).ToArray();
                if (values.Any(v => v is null))
                    continue;

                var key = new KeyValues(values);
                if (AddedKeysOf(added, foreignKey.PrincipalKey, addedKeys).Contains(key))
                    continue;

                if (!references.TryGetValue(foreignKey.PrincipalKey, out var byKey))
                    references[foreignKey.PrincipalKey] = byKey = [];
                if (!byKey.TryGetValue(key, out var list))
                    byKey[key] = list = [];
                list.Add((entry, foreignKey));
            }
        }

        foreach (var (principalKey, byKey) in references)
        {
            var root = principalKey.DeclaringEntityType.GetRootType();

            // Fail fast without a tenant, before querying the database.
            var current = guard.RequireCurrentTenant(root.ClrType);

            var foreign = await StoredTenantQuery.QueryAsync(
                context, root, principalKey.Properties, byKey.Keys, otherThanTenant: current, async, cancellationToken);

            if (foreign.Count == 0)
                continue;

            var (rowKey, tenantId) = foreign[0];

            var (entry, foreignKey) = StoredTenantQuery.FindMatch(byKey, rowKey)[0];

            guard.VerifyReference(
                entry.Entity,
                foreignKey.PrincipalEntityType.ClrType,
                foreignKey.Properties.Select(p => p.Name).ToList(),
                tenantId);
        }
    }

    private static IForeignKey[] GetForeignKeys(IEntityType entityType) =>
        ForeignKeys.GetValue(entityType, type => type.GetForeignKeys().Where(TenantForeignKeys.IsUnscoped).ToArray());

    /// <summary>
    /// The values of <paramref name="principalKey"/> of every entity added in this commit to its hierarchy.
    /// </summary>
    private static HashSet<KeyValues> AddedKeysOf(
        IReadOnlyList<EntityEntry> added,
        IKey principalKey,
        Dictionary<IKey, HashSet<KeyValues>> cache)
    {
        if (cache.TryGetValue(principalKey, out var keys))
            return keys;

        var principalType = principalKey.DeclaringEntityType;

        keys = added
            .Where(e => principalType.IsAssignableFrom(e.Metadata))
            .Select(e => new KeyValues(principalKey.Properties.Select(p => e.Property(p.Name).CurrentValue).ToArray()))
            .ToHashSet();

        cache[principalKey] = keys;
        return keys;
    }
}

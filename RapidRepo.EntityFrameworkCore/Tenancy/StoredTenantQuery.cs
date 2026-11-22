using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using RapidRepo.Tenancy;
using System.Linq.Expressions;
using System.Reflection;

namespace RapidRepo.EntityFrameworkCore.Internal;

/// <summary>
/// Reads the tenant stored in rows, for the commit's checks: one <c>SELECT key, TenantId</c> per entity hierarchy
/// (and batch of keys), for the matching rows of other tenants, with every query filter lifted, so a row is found
/// whatever its deletion state. Used for the entities about to be updated or deleted, and for the rows that foreign
/// keys refer to.
/// </summary>
/// <remarks>
/// <para>
/// The entity instance is never trusted for this: <c>DbSet.Update</c> of an instance the context never loaded takes
/// the caller's values as the original ones, so its <c>TenantId</c> proves nothing.
/// </para>
/// <para>
/// The database compares the keys, not this class: the <c>UPDATE</c> or <c>DELETE</c> matches rows with the
/// database's collation, so a case-insensitive one would let <c>"ABC"</c> reach another tenant's <c>"abc"</c> if the
/// keys were compared exactly here.
/// </para>
/// </remarks>
internal static class StoredTenantQuery
{
    /// <summary>Keys per query for a single-column key, sent as one <c>IN</c> list parameter.</summary>
    private const int SingleKeyBatchSize = 500;

    /// <summary>
    /// Keys per query for a composite key. Each key becomes an <c>(a = @p AND b = @q)</c> term with one parameter
    /// per column, so batches are smaller to stay well under the parameter limits of the databases (2100 on SQL
    /// Server) and keep the generated SQL readable.
    /// </summary>
    private const int CompositeKeyBatchSize = 100;

    private static readonly MethodInfo QueryMethod =
        typeof(StoredTenantQuery).GetMethod(nameof(Query), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly MethodInfo PropertyMethod = typeof(EF).GetMethod(nameof(EF.Property))!;

    /// <summary>
    /// Returns the entries whose row belongs to another tenant than <paramref name="currentTenant"/>, with the
    /// tenant stored in that row. Entries whose row is the current tenant's, or is missing, are left out.
    /// </summary>
    public static async Task<Dictionary<EntityEntry, object?>> LoadOtherTenantsAsync(
        DbContext context,
        IReadOnlyList<EntityEntry> entries,
        object currentTenant,
        bool async,
        CancellationToken cancellationToken)
    {
        var foreign = new Dictionary<EntityEntry, object?>();

        foreach (var hierarchy in entries.GroupBy(e => e.Metadata.GetRootType()))
        {
            var root = hierarchy.Key;
            var keyProperties = root.FindPrimaryKey()?.Properties
                ?? throw new InvalidOperationException($"{root.ClrType.Name} has no primary key, so its tenant cannot be verified.");

            var byKey = new Dictionary<KeyValues, List<EntityEntry>>();
            foreach (var entry in hierarchy)
            {
                var key = new KeyValues(keyProperties.Select(p => entry.Property(p.Name).OriginalValue).ToArray());
                if (!byKey.TryGetValue(key, out var list))
                    byKey[key] = list = [];
                list.Add(entry);
            }

            var rows = await QueryAsync(context, root, keyProperties, byKey.Keys, currentTenant, async, cancellationToken);

            foreach (var (key, tenantId) in rows)
            {
                foreach (var entry in FindMatch(byKey, key))
                    foreign[entry] = tenantId;
            }
        }

        return foreign;
    }

    /// <summary>
    /// The value of <paramref name="byKey"/> for a key the database returned. The database may have matched it with
    /// a collation that an exact comparison misses, so this falls back to comparing strings without case or
    /// trailing spaces, and then to the first value: the row matched one of the keys, so one of them is affected.
    /// </summary>
    /// <remarks>
    /// The last fallback only decides which entry the error message names, never whether the commit fails: with a
    /// collation the loose comparison does not model (accents, for example), the message may name another entry of
    /// the same batch than the one that matched.
    /// </remarks>
    public static TValue FindMatch<TValue>(Dictionary<KeyValues, TValue> byKey, KeyValues key)
    {
        if (byKey.TryGetValue(key, out var exact))
            return exact;

        foreach (var (candidate, value) in byKey)
        {
            if (candidate.Values.Length == key.Values.Length
                && candidate.Values.Zip(key.Values).All(pair => LooselyEqual(pair.First, pair.Second)))
            {
                return value;
            }
        }

        return byKey.Values.First();
    }

    private static bool LooselyEqual(object? a, object? b) =>
        a is string x && b is string y
            ? string.Equals(x.TrimEnd(), y.TrimEnd(), StringComparison.OrdinalIgnoreCase)
            : Equals(a, b);

    /// <summary>
    /// Returns the key and stored tenant of the rows of <paramref name="root"/>'s hierarchy whose
    /// <paramref name="keyProperties"/> match one of <paramref name="keys"/>, with every query filter lifted. With
    /// <paramref name="otherThanTenant"/>, only the rows of other tenants are returned, so the database compares the
    /// keys with its own collation, the way it enforces foreign keys.
    /// </summary>
    public static async Task<List<(KeyValues Key, object? TenantId)>> QueryAsync(
        DbContext context,
        IEntityType root,
        IReadOnlyList<IProperty> keyProperties,
        IReadOnlyCollection<KeyValues> keys,
        object? otherThanTenant,
        bool async,
        CancellationToken cancellationToken)
    {
        var result = new List<(KeyValues, object?)>();

        foreach (var batch in keys.Chunk(keyProperties.Count == 1 ? SingleKeyBatchSize : CompositeKeyBatchSize))
        {
            var query = (IQueryable<object?[]>)QueryMethod
                .MakeGenericMethod(root.ClrType)
                .Invoke(null, [context, keyProperties, batch, otherThanTenant])!;

            var rows = async ? await query.ToListAsync(cancellationToken) : query.ToList();

            foreach (var row in rows)
                result.Add((new KeyValues(row[..^1]), row[^1]));
        }

        return result;
    }

    private static IQueryable<object?[]> Query<TRoot>(
        DbContext context,
        IReadOnlyList<IProperty> keyProperties,
        KeyValues[] keys,
        object? otherThanTenant)
        where TRoot : class
    {
        var entity = Expression.Parameter(typeof(TRoot), "e");

        Expression PropertyOf(string name, Type type) =>
            Expression.Call(PropertyMethod.MakeGenericMethod(type), entity, Expression.Constant(name));

        Expression predicate;

        if (keyProperties.Count == 1)
        {
            var keyType = keyProperties[0].ClrType;
            var values = Array.CreateInstance(keyType, keys.Length);
            for (var i = 0; i < keys.Length; i++)
                values.SetValue(keys[i].Values[0], i);

            predicate = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                [keyType],
                Expression.Constant(values),
                PropertyOf(keyProperties[0].Name, keyType));
        }
        else
        {
            predicate = keys
                .Select(key => keyProperties
                    .Select((p, i) => (Expression)Expression.Equal(
                        PropertyOf(p.Name, p.ClrType),
                        Expression.Constant(key.Values[i], p.ClrType)))
                    .Aggregate(Expression.AndAlso))
                .Aggregate(Expression.OrElse);
        }

        var tenantType = context.Model.FindEntityType(typeof(TRoot))!
            .FindProperty(TenantTraits.TenantIdPropertyName)!.ClrType;

        if (otherThanTenant is not null)
        {
            predicate = Expression.AndAlso(
                predicate,
                Expression.NotEqual(
                    PropertyOf(TenantTraits.TenantIdPropertyName, tenantType),
                    Expression.Constant(otherThanTenant, tenantType)));
        }

        var projection = Expression.NewArrayInit(
            typeof(object),
            keyProperties
                .Select(p => (Expression)Expression.Convert(PropertyOf(p.Name, p.ClrType), typeof(object)))
                .Append(Expression.Convert(PropertyOf(TenantTraits.TenantIdPropertyName, tenantType), typeof(object))));

        return context.Set<TRoot>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(Expression.Lambda<Func<TRoot, bool>>(predicate, entity))
            .Select(Expression.Lambda<Func<TRoot, object?[]>>(projection, entity));
    }
}

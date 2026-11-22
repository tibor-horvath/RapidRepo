using RapidRepo.Entities.Interfaces;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;

namespace RapidRepo.Tenancy;

/// <summary>
/// Facts about tenant entities, resolved once per type and shared by every provider.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class TenantTraits
{
    /// <summary>
    /// The name of the <see cref="ITenantEntity{TTenantKey}.TenantId"/> property, as mapped by every provider.
    /// </summary>
    public const string TenantIdPropertyName = nameof(ITenantEntity<int>.TenantId);

    private static readonly ConcurrentDictionary<Type, Accessor?> Accessors = new();

    /// <summary>
    /// Gets the tenant key type of <paramref name="entityType"/>: the <c>TTenantKey</c> of the
    /// <see cref="ITenantEntity{TTenantKey}"/> it implements.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns>The tenant key type, or <see langword="null"/> when the type is not a tenant entity.</returns>
    /// <exception cref="InvalidOperationException">The type implements <see cref="ITenantEntity{TTenantKey}"/> more than once.</exception>
    public static Type? GetTenantKeyType(Type entityType) => GetAccessor(entityType)?.KeyType;

    /// <summary>
    /// Gets a value indicating whether <paramref name="entityType"/> implements <see cref="ITenantEntity{TTenantKey}"/>.
    /// </summary>
    /// <param name="entityType">The entity type.</param>
    /// <returns><see langword="true"/> for a tenant entity.</returns>
    public static bool IsTenantEntity(Type entityType) => GetTenantKeyType(entityType) is not null;

    /// <summary>
    /// Reads the <c>TenantId</c> of a tenant entity, boxed.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns>The tenant id.</returns>
    /// <exception cref="ArgumentException"><paramref name="entity"/> is not a tenant entity.</exception>
    public static object? GetTenantId(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return RequireAccessor(entity.GetType(), nameof(entity)).Get(entity);
    }

    /// <summary>
    /// Sets the <c>TenantId</c> of a tenant entity.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <param name="tenantId">The tenant id, of the entity's tenant key type.</param>
    /// <exception cref="ArgumentException"><paramref name="entity"/> is not a tenant entity.</exception>
    public static void SetTenantId(object entity, object tenantId)
    {
        ArgumentNullException.ThrowIfNull(entity);
        RequireAccessor(entity.GetType(), nameof(entity)).Set(entity, tenantId);
    }

    private static Accessor RequireAccessor(Type entityType, string paramName) =>
        GetAccessor(entityType)
        ?? throw new ArgumentException(
            $"'{entityType.Name}' does not implement {nameof(ITenantEntity<int>)}<TTenantKey>.", paramName);

    private static Accessor? GetAccessor(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        return Accessors.GetOrAdd(entityType, BuildAccessor);
    }

    private static Accessor? BuildAccessor(Type entityType)
    {
        var interfaces = entityType
            .GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITenantEntity<>))
            .ToList();

        if (interfaces.Count == 0)
            return null;

        if (interfaces.Count > 1)
        {
            throw new InvalidOperationException(
                $"'{entityType.Name}' implements {nameof(ITenantEntity<int>)}<TTenantKey> more than once " +
                $"({string.Join(", ", interfaces.Select(i => i.GenericTypeArguments[0].Name))}). An entity belongs to " +
                "one tenant, so it must have one tenant key type.");
        }

        var tenantInterface = interfaces[0];
        var property = tenantInterface.GetProperty(TenantIdPropertyName)!;

        var entity = Expression.Parameter(typeof(object), "entity");
        var value = Expression.Parameter(typeof(object), "value");
        var typed = Expression.Convert(entity, tenantInterface);

        var get = Expression.Lambda<Func<object, object?>>(
            Expression.Convert(Expression.Property(typed, property), typeof(object)), entity).Compile();

        var set = Expression.Lambda<Action<object, object>>(
            Expression.Assign(Expression.Property(typed, property), Expression.Convert(value, property.PropertyType)),
            entity,
            value).Compile();

        return new Accessor(tenantInterface.GenericTypeArguments[0], get, set);
    }

    private sealed record Accessor(Type KeyType, Func<object, object?> Get, Action<object, object> Set);
}

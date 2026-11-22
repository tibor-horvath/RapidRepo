using RapidRepo.Tenancy;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// Reads <see cref="ITenantDbContext{TTenantKey}.CurrentTenantId"/> when the key type is only known at run time,
/// through a delegate compiled once per key type.
/// </summary>
internal static class TenantDbContexts
{
    private static readonly ConcurrentDictionary<Type, Accessor> Accessors = new();

    /// <summary>
    /// Reads the context's own answer, without overrides or normalization.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="tenantKeyType">The tenant key type.</param>
    /// <param name="tenantId">The context's answer, boxed.</param>
    /// <returns>
    /// <see langword="false"/> when <paramref name="context"/> does not implement
    /// <see cref="ITenantDbContext{TTenantKey}"/> for <paramref name="tenantKeyType"/>.
    /// </returns>
    public static bool TryGetContextTenantId(object context, Type tenantKeyType, out object? tenantId)
    {
        var accessor = GetAccessor(tenantKeyType);

        if (!accessor.InterfaceType.IsInstanceOfType(context))
        {
            tenantId = null;
            return false;
        }

        tenantId = accessor.Get(context);
        return true;
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="context"/> implements
    /// <see cref="ITenantDbContext{TTenantKey}"/> for <paramref name="tenantKeyType"/>. Never reads
    /// <c>CurrentTenantId</c>, so it never runs the application's tenant provider.
    /// </summary>
    public static bool Implements(object context, Type tenantKeyType) =>
        GetAccessor(tenantKeyType).InterfaceType.IsInstanceOfType(context);

    /// <summary>
    /// The current tenant for a read: the innermost override, else the context's.
    /// </summary>
    /// <exception cref="TenantViolationException">The context returns an empty string.</exception>
    public static object? GetCurrentTenantId(object context, Type tenantKeyType) =>
        AmbientTenant.ResolveCurrentTenant(
            tenantKeyType,
            () => TryGetContextTenantId(context, tenantKeyType, out var tenantId) ? tenantId : null,
            SourceName(context));

    /// <summary>
    /// Names the context's <c>CurrentTenantId</c> in error messages.
    /// </summary>
    public static string SourceName(object context) =>
        $"{context.GetType().Name}.{nameof(ITenantDbContext<int>.CurrentTenantId)}";

    private static Accessor GetAccessor(Type tenantKeyType) => Accessors.GetOrAdd(tenantKeyType, BuildAccessor);

    private static Accessor BuildAccessor(Type tenantKeyType)
    {
        var interfaceType = typeof(ITenantDbContext<>).MakeGenericType(tenantKeyType);
        var context = Expression.Parameter(typeof(object), "context");
        var property = Expression.Property(
            Expression.Convert(context, interfaceType),
            nameof(ITenantDbContext<int>.CurrentTenantId));

        var get = Expression.Lambda<Func<object, object?>>(Expression.Convert(property, typeof(object)), context).Compile();
        return new Accessor(interfaceType, get);
    }

    private sealed record Accessor(Type InterfaceType, Func<object, object?> Get);
}

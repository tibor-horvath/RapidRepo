using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq.Expressions;

namespace RapidRepo.Tenancy;

/// <summary>
/// Calls an <see cref="ICurrentTenantProvider{TTenantKey}"/> whose key type is only known at run time.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class CurrentTenantProviders
{
    private static readonly ConcurrentDictionary<Type, Func<object, object?>> Invokers = new();

    /// <summary>
    /// Asks <paramref name="provider"/> for the current tenant.
    /// </summary>
    /// <param name="provider">An <see cref="ICurrentTenantProvider{TTenantKey}"/> for <paramref name="tenantKeyType"/>.</param>
    /// <param name="tenantKeyType">The tenant key type.</param>
    /// <returns>The provider's answer, boxed.</returns>
    public static object? GetCurrentTenantId(object provider, Type tenantKeyType)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(tenantKeyType);

        return Invokers.GetOrAdd(tenantKeyType, BuildInvoker)(provider);
    }

    private static Func<object, object?> BuildInvoker(Type tenantKeyType)
    {
        var providerType = typeof(ICurrentTenantProvider<>).MakeGenericType(tenantKeyType);
        var provider = Expression.Parameter(typeof(object), "provider");
        var call = Expression.Call(
            Expression.Convert(provider, providerType),
            providerType.GetMethod(nameof(ICurrentTenantProvider<int>.GetCurrentTenantId))!);

        return Expression.Lambda<Func<object, object?>>(Expression.Convert(call, typeof(object)), provider).Compile();
    }
}

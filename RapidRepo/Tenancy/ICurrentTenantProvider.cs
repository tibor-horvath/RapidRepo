namespace RapidRepo.Tenancy;

/// <summary>
/// Supplies the tenant of the current request or scope, for example from a claim, a header or the host name.
/// </summary>
/// <remarks>
/// <para>
/// The method is synchronous on purpose: it runs inside reads and commits, and the usual sources (the current
/// <c>HttpContext</c>, an ambient scope) are already in memory.
/// </para>
/// <para>
/// Without a current tenant, reading or writing a tenant entity throws a <see cref="TenantViolationException"/>.
/// Code that legitimately runs without one, such as a background job or seeding, opens an explicit scope:
/// <see cref="ITenantScope{TTenantKey}.Override"/> to act as one tenant, or
/// <see cref="Host.ITenantHostScope.BeginHostScope"/> to act across tenants.
/// </para>
/// </remarks>
/// <typeparam name="TTenantKey">The type of the tenant key.</typeparam>
public interface ICurrentTenantProvider<TTenantKey>
    where TTenantKey : notnull
{
    /// <summary>
    /// Gets the tenant of the current request or scope.
    /// </summary>
    /// <returns>
    /// The tenant, or <see langword="null"/> when there is none. For a value-type key, where <c>TTenantKey?</c> is
    /// <typeparamref name="TTenantKey"/> itself, the default value (for example <see cref="Guid.Empty"/> or 0) means
    /// none. An empty string is a misconfiguration and makes reads and commits throw.
    /// </returns>
    TTenantKey? GetCurrentTenantId();
}

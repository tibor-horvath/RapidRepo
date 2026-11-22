namespace RapidRepo.Tenancy;

/// <summary>
/// Replaces the current tenant for a block of code, for example a background job that processes one tenant at a time.
/// </summary>
/// <typeparam name="TTenantKey">The type of the tenant key.</typeparam>
public interface ITenantScope<TTenantKey>
    where TTenantKey : notnull
{
    /// <summary>
    /// Acts as <paramref name="tenantId"/> until the returned scope is disposed, whatever the tenant provider returns.
    /// Reads see only that tenant's rows and commits stamp and guard against it.
    /// </summary>
    /// <remarks>
    /// Scopes flow with the asynchronous context (like <see cref="AsyncLocal{T}"/>), nest, and restore the previous
    /// scope when disposed. An override narrows access to a single tenant, so it is not subject to a
    /// <see cref="Host.ITenantHostScopePolicy"/>.
    /// </remarks>
    /// <param name="tenantId">The tenant to act as.</param>
    /// <returns>The scope. Dispose it, with <c>using</c>, to restore the previous one.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="tenantId"/> is the default value or an empty string, neither of which is a tenant.
    /// </exception>
    IDisposable Override(TTenantKey tenantId);
}

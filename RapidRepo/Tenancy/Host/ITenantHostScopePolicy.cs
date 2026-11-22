namespace RapidRepo.Tenancy.Host;

/// <summary>
/// Decides whether a host scope may be opened. Register one to deny host scopes where they don't belong, for example
/// inside HTTP requests. Without one, host scopes are allowed.
/// </summary>
public interface ITenantHostScopePolicy
{
    /// <summary>
    /// Decides whether a host scope may be opened now.
    /// </summary>
    /// <param name="request">Where the scope is being opened.</param>
    /// <returns><see langword="true"/> to allow it; <see langword="false"/> to make it throw.</returns>
    bool IsAllowed(TenantHostScopeRequest request);
}

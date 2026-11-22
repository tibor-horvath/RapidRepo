using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RapidRepo.Tenancy.Host;

/// <summary>
/// The default <see cref="ITenantHostScope"/>. <c>AddRapidRepo</c> registers it, with logging; without dependency
/// injection, create one directly.
/// </summary>
/// <param name="policy">The policy that decides whether a host scope may be opened, or <see langword="null"/> to allow all.</param>
public sealed class TenantHostScope(ITenantHostScopePolicy? policy = null) : ITenantHostScope
{
    /// <summary>
    /// The tag set on the current <see cref="Activity"/> when a host scope is opened, so host access shows up in traces.
    /// </summary>
    public const string HostScopeActivityTag = "rapidrepo.tenant.host_scope";

    /// <summary>
    /// The tag carrying the caller of the host scope on the current <see cref="Activity"/>.
    /// </summary>
    public const string HostScopeCallerActivityTag = "rapidrepo.tenant.host_scope.caller";

    /// <inheritdoc />
    public IDisposable BeginHostScope(
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var request = new TenantHostScopeRequest(callerMemberName, callerFilePath, callerLineNumber);

        if (policy is not null && !policy.IsAllowed(request))
        {
            throw new TenantViolationException(
                $"A host scope was denied by the registered {nameof(ITenantHostScopePolicy)} ({policy.GetType().Name}) " +
                $"at {request}.");
        }

        if (Activity.Current is { } activity)
        {
            activity.SetTag(HostScopeActivityTag, true);
            activity.SetTag(HostScopeCallerActivityTag, request.ToString());
        }

        return AmbientTenant.Push(isHost: true, tenantKeyType: null, tenantId: null);
    }
}

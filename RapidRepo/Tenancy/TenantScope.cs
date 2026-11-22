using System.Diagnostics;

namespace RapidRepo.Tenancy;

/// <summary>
/// The default <see cref="ITenantScope{TTenantKey}"/>. <c>AddRapidRepo</c> registers it; without dependency injection,
/// create one directly.
/// </summary>
/// <typeparam name="TTenantKey">The type of the tenant key.</typeparam>
public sealed class TenantScope<TTenantKey> : ITenantScope<TTenantKey>
    where TTenantKey : notnull
{
    /// <summary>
    /// The tag set on the current <see cref="Activity"/> when a tenant override is opened.
    /// </summary>
    public const string OverrideActivityTag = "rapidrepo.tenant.override";

    /// <inheritdoc />
    public IDisposable Override(TTenantKey tenantId)
    {
        if (TenantKeys.IsNone(tenantId))
        {
            throw new ArgumentException(
                $"'{tenantId}' is not a tenant: the default value and an empty string mean 'no tenant'.",
                nameof(tenantId));
        }

        Activity.Current?.SetTag(OverrideActivityTag, true);
        return AmbientTenant.Push(isHost: false, typeof(TTenantKey), tenantId);
    }
}

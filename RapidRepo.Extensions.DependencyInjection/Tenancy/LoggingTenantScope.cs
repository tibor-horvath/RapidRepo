using Microsoft.Extensions.Logging;
using RapidRepo.Tenancy;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

/// <summary>
/// The registered <see cref="ITenantScope{TTenantKey}"/>: logs every override, then opens it.
/// </summary>
internal sealed class LoggingTenantScope<TTenantKey>(ILoggerFactory? loggerFactory = null) : ITenantScope<TTenantKey>
    where TTenantKey : notnull
{
    private readonly TenantScope<TTenantKey> _inner = new();
    private readonly ILogger _logger = TenancyLog.Create(loggerFactory);

    public IDisposable Override(TTenantKey tenantId)
    {
        var scope = _inner.Override(tenantId);
        _logger.LogInformation("RapidRepo tenant override opened for tenant {TenantId}.", tenantId);
        return scope;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RapidRepo.Tenancy.Host;
using System.Runtime.CompilerServices;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

/// <summary>
/// The registered <see cref="ITenantHostScope"/>: applies the optional <see cref="ITenantHostScopePolicy"/>, logs
/// every host scope with its caller, then opens it.
/// </summary>
internal sealed class LoggingTenantHostScope(
    IServiceProvider services,
    ILoggerFactory? loggerFactory = null) : ITenantHostScope
{
    private readonly TenantHostScope _inner = new(services.GetService<ITenantHostScopePolicy>());
    private readonly ILogger _logger = TenancyLog.Create(loggerFactory);

    public IDisposable BeginHostScope(
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0)
    {
        var scope = _inner.BeginHostScope(callerMemberName, callerFilePath, callerLineNumber);
        _logger.LogInformation(
            "RapidRepo host scope opened by {CallerMemberName} ({CallerFilePath}:{CallerLineNumber}); tenant filters and write guards are lifted.",
            callerMemberName,
            callerFilePath,
            callerLineNumber);
        return scope;
    }
}

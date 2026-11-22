using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RapidRepo.Extensions.DependencyInjection.Internal;

/// <summary>
/// The logger the tenant scopes write to.
/// </summary>
internal static class TenancyLog
{
    /// <summary>The log category of every tenant scope.</summary>
    public const string Category = "RapidRepo.Tenancy";

    public static ILogger Create(ILoggerFactory? loggerFactory) =>
        (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger(Category);
}

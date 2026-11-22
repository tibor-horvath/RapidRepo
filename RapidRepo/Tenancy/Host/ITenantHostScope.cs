using System.Runtime.CompilerServices;

namespace RapidRepo.Tenancy.Host;

/// <summary>
/// Opens a host scope: a block of code that acts across tenants, such as a migration, seeding, or a job that loops
/// over every tenant. Inside it, tenant filters and write guards are lifted.
/// </summary>
/// <remarks>
/// This type lives in its own namespace on purpose, so that request-handling code can be kept from using it with an
/// analyzer or an architecture test.
/// </remarks>
public interface ITenantHostScope
{
    /// <summary>
    /// Acts as the host until the returned scope is disposed: reads return every tenant's rows, and commits skip the
    /// tenant rules. Entities added inside it must have their <c>TenantId</c> set, because there is no tenant to
    /// stamp.
    /// </summary>
    /// <param name="callerMemberName">Supplied by the compiler.</param>
    /// <param name="callerFilePath">Supplied by the compiler.</param>
    /// <param name="callerLineNumber">Supplied by the compiler.</param>
    /// <returns>The scope. Dispose it, with <c>using</c>, to restore the previous one.</returns>
    /// <exception cref="TenantViolationException">A registered <see cref="ITenantHostScopePolicy"/> denies it.</exception>
    IDisposable BeginHostScope(
        [CallerMemberName] string callerMemberName = "",
        [CallerFilePath] string callerFilePath = "",
        [CallerLineNumber] int callerLineNumber = 0);
}

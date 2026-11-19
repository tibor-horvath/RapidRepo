using Microsoft.EntityFrameworkCore;

namespace RapidRepo.EntityFrameworkCore.DependencyInjection;

/// <summary>
/// Options for the EF Core provider configured by <c>UseEntityFrameworkCore()</c>.
/// </summary>
public sealed class EntityFrameworkCoreProviderOptions
{
    /// <summary>
    /// Throw instead of warn when registered types depend on the base <see cref="DbContext"/> but no context is
    /// available to satisfy it: the call is not bound to a context with <c>UseEntityFrameworkCore&lt;TContext&gt;()</c>
    /// and there is no existing <see cref="DbContext"/> registration. Default: <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Off by default because the check runs during <c>AddRapidRepo</c>: an application that registers its
    /// <see cref="DbContext"/> afterwards is correct but cannot be seen as such at that point.
    /// </remarks>
    public bool ThrowOnMissingDbContext { get; set; }
}

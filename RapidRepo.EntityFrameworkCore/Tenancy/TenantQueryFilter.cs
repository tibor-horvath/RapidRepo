using RapidRepo.Tenancy;
using System.ComponentModel;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// The values the tenant query filter reads. Not for direct use: the filter that <c>AddTenantQueryFilters</c>
/// builds calls these members with the context, which makes EF Core evaluate them on every query rather than once
/// when the model is built.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TenantQueryFilter
{
    /// <summary>
    /// Gets a value indicating whether the query runs in a host scope, which lifts the tenant filter.
    /// </summary>
    /// <remarks>
    /// <paramref name="context"/> is not read: taking it is what makes EF Core evaluate the call per query.
    /// </remarks>
    /// <typeparam name="TTenantKey">The tenant key type.</typeparam>
    /// <param name="context">The context the query runs on.</param>
    /// <returns><see langword="true"/> inside a host scope.</returns>
    public static bool IsHostScope<TTenantKey>(ITenantDbContext<TTenantKey> context)
        where TTenantKey : notnull
        => AmbientTenant.IsHostScope;

    /// <summary>
    /// Gets the tenant the query is filtered to: the innermost tenant override, else the context's
    /// <see cref="ITenantDbContext{TTenantKey}.CurrentTenantId"/>.
    /// </summary>
    /// <remarks>
    /// An empty string matches no rows rather than throwing here: the repository's read check reports it.
    /// </remarks>
    /// <typeparam name="TTenantKey">The tenant key type.</typeparam>
    /// <param name="context">The context the query runs on.</param>
    /// <returns>The tenant, boxed, or <see langword="null"/> when there is none, which matches no rows.</returns>
    public static object? CurrentTenantId<TTenantKey>(ITenantDbContext<TTenantKey> context)
        where TTenantKey : notnull
    {
        ArgumentNullException.ThrowIfNull(context);
        return AmbientTenant.ResolveCurrentTenantOrNull(typeof(TTenantKey), () => context.CurrentTenantId);
    }
}
